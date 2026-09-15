using System;
using System.IO;
using System.Threading.Tasks;
using Renci.SshNet;
using SIAPPDeployment.Configuration;
using SIAPPDeployment.Models;
using SIAPPDeployment.Utilities;

namespace SIAPPDeployment.Services
{
    public interface IReleaseActivationService
    {
        Task<StepResult> ActivateReleaseAsync(DeploymentInfo info, string password);
        Task<StepResult> RollbackReleaseAsync(string previousVersion, string password);
    }

    public class ReleaseActivationService : IReleaseActivationService
    {
        private readonly ServerSettings _settings;

        public ReleaseActivationService(DeploymentSettings settings)
        {
            _settings = settings.Server;
        }

        public async Task<StepResult> ActivateReleaseAsync(DeploymentInfo info, string password)
        {
            var startTime = DateTime.Now;
            var result = new StepResult
            {
                StepNumber = 5,
                StepName = "Activando nueva versión"
            };

            if (info.IsDryRun)
            {
                ConsoleHelper.PrintStepSkipped("[SIMULACIÓN] Activación de release en servidor omitida.");
                result.Success = true;
                result.Duration = TimeSpan.Zero;
                return result;
            }

            try
            {
                var connectionInfo = new ConnectionInfo(
                    _settings.Host,
                    _settings.Port,
                    _settings.Username,
                    new PasswordAuthenticationMethod(_settings.Username, password)
                );

                using var ssh = new SshClient(connectionInfo);
                await Task.Run(() => ssh.Connect());

                string remoteReleaseDir = $"{_settings.ReleasesPath}\\{info.Version}".Replace("/", "\\");
                string remoteZip = info.RemoteZipPath.Replace("/", "\\");
                info.RemoteReleaseFolder = remoteReleaseDir;

                // 1. Script PowerShell para descomprimir en el servidor
                string unzipCmd = $"powershell -NoProfile -Command \"Expand-Archive -Path '{remoteZip}' -DestinationPath '{remoteReleaseDir}' -Force\"";
                var unzipResult = await ExecuteRemoteCommandAsync(ssh, unzipCmd);
                if (unzipResult.ExitStatus != 0)
                {
                    result.Success = false;
                    result.ErrorMessage = $"Error al descomprimir en el servidor: {unzipResult.Error}";
                    ConsoleHelper.PrintStepFailed(result.ErrorMessage);
                    return result;
                }

                // 2. Colocar app_offline.htm en Backend si está configurado
                if (_settings.AppOfflineEnabled && !string.IsNullOrEmpty(_settings.CurrentBackendPath))
                {
                    string offlineCmd = $"powershell -NoProfile -Command \"Set-Content -Path '{_settings.CurrentBackendPath}\\app_offline.htm' -Value '<html><head><meta charset=\\\"utf-8\\\"></head><body><h2>SIAPP - Actualizando Sistema...</h2></body></html>'\"";
                    await ExecuteRemoteCommandAsync(ssh, offlineCmd);
                    await Task.Delay(2000); // Esperar liberación de handles por IIS
                }

                // 3. Sincronizar archivos de Backend hacia el directorio activo
                if (!string.IsNullOrEmpty(_settings.CurrentBackendPath))
                {
                    string copyBackendCmd = $"powershell -NoProfile -Command \"Copy-Item -Path '{remoteReleaseDir}\\backend\\*' -Destination '{_settings.CurrentBackendPath}' -Recurse -Force\"";
                    var copyBackendResult = await ExecuteRemoteCommandAsync(ssh, copyBackendCmd);
                    if (copyBackendResult.ExitStatus != 0)
                    {
                        result.Success = false;
                        result.ErrorMessage = $"Error al copiar archivos de Backend a {_settings.CurrentBackendPath}: {copyBackendResult.Error}";
                        ConsoleHelper.PrintStepFailed(result.ErrorMessage);
                        return result;
                    }
                }

                // 4. Sincronizar archivos de Frontend hacia el directorio activo
                if (!string.IsNullOrEmpty(_settings.CurrentFrontendPath))
                {
                    string copyFrontendCmd = $"powershell -NoProfile -Command \"Copy-Item -Path '{remoteReleaseDir}\\frontend\\*' -Destination '{_settings.CurrentFrontendPath}' -Recurse -Force\"";
                    var copyFrontendResult = await ExecuteRemoteCommandAsync(ssh, copyFrontendCmd);
                    if (copyFrontendResult.ExitStatus != 0)
                    {
                        result.Success = false;
                        result.ErrorMessage = $"Error al copiar archivos de Frontend a {_settings.CurrentFrontendPath}: {copyFrontendResult.Error}";
                        ConsoleHelper.PrintStepFailed(result.ErrorMessage);
                        return result;
                    }
                }

                // 5. Remover app_offline.htm para iniciar la nueva versión en IIS
                if (_settings.AppOfflineEnabled && !string.IsNullOrEmpty(_settings.CurrentBackendPath))
                {
                    string removeOfflineCmd = $"powershell -NoProfile -Command \"if (Test-Path '{_settings.CurrentBackendPath}\\app_offline.htm') {{ Remove-Item '{_settings.CurrentBackendPath}\\app_offline.htm' -Force }}\"";
                    await ExecuteRemoteCommandAsync(ssh, removeOfflineCmd);
                }

                ssh.Disconnect();

                result.Success = true;
                result.Duration = DateTime.Now - startTime;
                ConsoleHelper.PrintStepSuccess($"Release {info.Version} activado exitosamente en IIS.");
                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = $"Error en la activación de release: {ex.Message}";
                ConsoleHelper.PrintStepFailed(result.ErrorMessage);
                return result;
            }
        }

        public async Task<StepResult> RollbackReleaseAsync(string previousVersion, string password)
        {
            var result = new StepResult { StepName = "Rollback de versión" };
            // Restaurar versión previa desde carpeta releases/{previousVersion}
            try
            {
                var connectionInfo = new ConnectionInfo(
                    _settings.Host,
                    _settings.Port,
                    _settings.Username,
                    new PasswordAuthenticationMethod(_settings.Username, password)
                );

                using var ssh = new SshClient(connectionInfo);
                await Task.Run(() => ssh.Connect());

                string remoteReleaseDir = $"{_settings.ReleasesPath}\\{previousVersion}".Replace("/", "\\");

                if (_settings.AppOfflineEnabled && !string.IsNullOrEmpty(_settings.CurrentBackendPath))
                {
                    await ExecuteRemoteCommandAsync(ssh, $"powershell -NoProfile -Command \"Set-Content -Path '{_settings.CurrentBackendPath}\\app_offline.htm' -Value '<h2>Restaurando versión anterior...</h2>'\"");
                    await Task.Delay(2000);
                }

                if (!string.IsNullOrEmpty(_settings.CurrentBackendPath))
                {
                    await ExecuteRemoteCommandAsync(ssh, $"powershell -NoProfile -Command \"Copy-Item -Path '{remoteReleaseDir}\\backend\\*' -Destination '{_settings.CurrentBackendPath}' -Recurse -Force\"");
                }

                if (!string.IsNullOrEmpty(_settings.CurrentFrontendPath))
                {
                    await ExecuteRemoteCommandAsync(ssh, $"powershell -NoProfile -Command \"Copy-Item -Path '{remoteReleaseDir}\\frontend\\*' -Destination '{_settings.CurrentFrontendPath}' -Recurse -Force\"");
                }

                if (_settings.AppOfflineEnabled && !string.IsNullOrEmpty(_settings.CurrentBackendPath))
                {
                    await ExecuteRemoteCommandAsync(ssh, $"powershell -NoProfile -Command \"if (Test-Path '{_settings.CurrentBackendPath}\\app_offline.htm') {{ Remove-Item '{_settings.CurrentBackendPath}\\app_offline.htm' -Force }}\"");
                }

                ssh.Disconnect();
                result.Success = true;
                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                return result;
            }
        }

        private static async Task<SshCommand> ExecuteRemoteCommandAsync(SshClient ssh, string commandText)
        {
            return await Task.Run(() =>
            {
                var cmd = ssh.CreateCommand(commandText);
                cmd.Execute();
                return cmd;
            });
        }
    }
}
