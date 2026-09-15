using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Renci.SshNet;
using SIAPPDeployment.Configuration;
using SIAPPDeployment.Models;
using SIAPPDeployment.Utilities;

namespace SIAPPDeployment.Services
{
    public interface ICleanupService
    {
        Task<StepResult> CleanupReleasesAsync(DeploymentInfo info, string password);
    }

    public class CleanupService : ICleanupService
    {
        private readonly DeploymentSettings _settings;

        public CleanupService(DeploymentSettings settings)
        {
            _settings = settings;
        }

        public async Task<StepResult> CleanupReleasesAsync(DeploymentInfo info, string password)
        {
            var startTime = DateTime.Now;
            var result = new StepResult
            {
                StepNumber = 8,
                StepName = "Limpiando versiones pasadas"
            };

            if (!_settings.Cleanup.Enabled)
            {
                ConsoleHelper.PrintStepSkipped("Limpieza de versiones desactivada en configuración.");
                result.Success = true;
                result.Duration = TimeSpan.Zero;
                return result;
            }

            if (info.IsDryRun)
            {
                ConsoleHelper.PrintStepSkipped("[SIMULACIÓN] Limpieza de versiones pasadas omitida.");
                result.Success = true;
                result.Duration = TimeSpan.Zero;
                return result;
            }

            try
            {
                int localRemovedCount = 0;
                int remoteRemovedCount = 0;

                // 1. Limpieza en máquina local
                if (_settings.Cleanup.CleanLocal && !string.IsNullOrWhiteSpace(_settings.Deployment.LocalReleasesPath))
                {
                    localRemovedCount = await Task.Run(() => CleanLocalReleases(info.Version, _settings.Cleanup.KeepPastReleasesCount));
                }

                // 2. Limpieza en Servidor Remoto vía SSH / PowerShell
                if (_settings.Cleanup.CleanRemote && !string.IsNullOrWhiteSpace(_settings.Server.Host))
                {
                    remoteRemovedCount = await CleanRemoteReleasesAsync(info.Version, _settings.Cleanup.KeepPastReleasesCount, password);
                }

                result.Success = true;
                result.Duration = DateTime.Now - startTime;
                ConsoleHelper.PrintStepSuccess($"Limpieza exitosa: {localRemovedCount} carpeta(s) local(es) y {remoteRemovedCount} remota(s) eliminada(s). Se conservó la versión actual y {_settings.Cleanup.KeepPastReleasesCount} de respaldo.");
                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = $"Error durante la limpieza de versiones: {ex.Message}";
                ConsoleHelper.PrintStepFailed(result.ErrorMessage);
                return result;
            }
        }

        private int CleanLocalReleases(string currentVersion, int keepPastCount)
        {
            int removedCount = 0;
            string localReleasesDir = _settings.Deployment.LocalReleasesPath;

            if (!Directory.Exists(localReleasesDir))
            {
                return 0;
            }

            var dirInfo = new DirectoryInfo(localReleasesDir);
            var subDirs = dirInfo.GetDirectories()
                .OrderByDescending(d => d.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var versionsToKeep = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { currentVersion };
            int pastCount = 0;

            foreach (var dir in subDirs)
            {
                if (!dir.Name.Equals(currentVersion, StringComparison.OrdinalIgnoreCase) && pastCount < keepPastCount)
                {
                    versionsToKeep.Add(dir.Name);
                    pastCount++;
                }
            }

            // Eliminar carpetas no conservadas
            foreach (var dir in subDirs)
            {
                if (!versionsToKeep.Contains(dir.Name))
                {
                    try
                    {
                        dir.Delete(true);
                        removedCount++;
                    }
                    catch { }
                }
            }

            // Eliminar archivos .zip asociados que no estén en versionsToKeep
            var zipFiles = dirInfo.GetFiles("SIAPP_*.zip");
            foreach (var zip in zipFiles)
            {
                string zipVersion = zip.Name.Replace("SIAPP_", "", StringComparison.OrdinalIgnoreCase);
                if (zipVersion.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    zipVersion = zipVersion[..^4];
                }

                if (!versionsToKeep.Contains(zipVersion))
                {
                    try
                    {
                        zip.Delete();
                    }
                    catch { }
                }
            }

            return removedCount;
        }

        private async Task<int> CleanRemoteReleasesAsync(string currentVersion, int keepPastCount, string password)
        {
            var server = _settings.Server;
            var connectionInfo = new ConnectionInfo(
                server.Host,
                server.Port,
                server.Username,
                new PasswordAuthenticationMethod(server.Username, password)
            );

            using var ssh = new SshClient(connectionInfo);
            await Task.Run(() => ssh.Connect());

            string releasesPath = server.ReleasesPath.Replace("/", "\\");
            string tempPath = server.TempPath.Replace("/", "\\");

            string script = $@"
$releasesDir = '{releasesPath}'
$tempDir = '{tempPath}'
$current = '{currentVersion}'
$keepPast = {keepPastCount}

$removed = 0
if (Test-Path $releasesDir) {{
    $allDirs = Get-ChildItem -Path $releasesDir -Directory | Sort-Object Name -Descending
    $toKeep = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    [void]$toKeep.Add($current)
    $pastCount = 0

    foreach ($d in $allDirs) {{
        if ($d.Name -ne $current -and $pastCount -lt $keepPast) {{
            [void]$toKeep.Add($d.Name)
            $pastCount++
        }}
    }}

    foreach ($d in $allDirs) {{
        if (-not $toKeep.Contains($d.Name)) {{
            Remove-Item -Path $d.FullName -Recurse -Force -ErrorAction SilentlyContinue
            $removed++
        }}
    }}

    if (Test-Path $tempDir) {{
        Get-ChildItem -Path $tempDir -Filter 'SIAPP_*.zip' | ForEach-Object {{
            $zipVer = $_.BaseName.Replace('SIAPP_', '')
            if (-not $toKeep.Contains($zipVer)) {{
                Remove-Item -Path $_.FullName -Force -ErrorAction SilentlyContinue
            }}
        }}
    }}
}}
Write-Output ""RELEASES_REMOVED:$removed""
";

            string escapedScript = script.Replace("\r\n", " ").Replace("\n", " ");
            string commandText = $"powershell -NoProfile -Command \"{escapedScript}\"";

            var command = await Task.Run(() =>
            {
                var cmd = ssh.CreateCommand(commandText);
                cmd.Execute();
                return cmd;
            });

            ssh.Disconnect();

            int remoteRemoved = 0;
            string output = command.Result ?? string.Empty;
            foreach (var line in output.Split('\n', '\r'))
            {
                if (line.StartsWith("RELEASES_REMOVED:", StringComparison.OrdinalIgnoreCase))
                {
                    string numStr = line.Replace("RELEASES_REMOVED:", "").Trim();
                    int.TryParse(numStr, out remoteRemoved);
                    break;
                }
            }

            return remoteRemoved;
        }
    }
}
