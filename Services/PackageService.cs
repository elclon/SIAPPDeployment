using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using SIAPPDeployment.Configuration;
using SIAPPDeployment.Models;
using SIAPPDeployment.Utilities;

namespace SIAPPDeployment.Services
{
    public interface IPackageService
    {
        string GenerateVersionIdentifier();
        Task<StepResult> PreparePackageAsync(DeploymentInfo info, string backendOutputFolder);
    }

    public class PackageService : IPackageService
    {
        private readonly DeploymentSettings _settings;

        public PackageService(DeploymentSettings settings)
        {
            _settings = settings;
        }

        public string GenerateVersionIdentifier()
        {
            string datePrefix = DateTime.Now.ToString("yyyy.MM.dd");
            string baseFolder = Path.Combine(_settings.Deployment.LocalReleasesPath, datePrefix);
            
            // Buscar correlativo del día
            int sequence = 1;
            while (Directory.Exists(Path.Combine(_settings.Deployment.LocalReleasesPath, $"{datePrefix}.{sequence:D3}")))
            {
                sequence++;
            }

            return $"{datePrefix}.{sequence:D3}";
        }

        public async Task<StepResult> PreparePackageAsync(DeploymentInfo info, string backendOutputFolder)
        {
            var startTime = DateTime.Now;
            var result = new StepResult
            {
                StepNumber = 3,
                StepName = "Preparando archivos"
            };

            if (info.IsDryRun)
            {
                ConsoleHelper.PrintStepSkipped("[SIMULACIÓN] Preparación de paquete omitida.");
                result.Success = true;
                result.Duration = TimeSpan.Zero;
                return result;
            }

            try
            {
                // 1. Crear estructura de carpetas de release
                info.LocalReleaseFolder = Path.Combine(_settings.Deployment.LocalReleasesPath, info.Version);
                string frontendTarget = Path.Combine(info.LocalReleaseFolder, "frontend");
                string backendTarget = Path.Combine(info.LocalReleaseFolder, "backend");

                if (Directory.Exists(info.LocalReleaseFolder))
                {
                    Directory.Delete(info.LocalReleaseFolder, true);
                }
                Directory.CreateDirectory(frontendTarget);
                Directory.CreateDirectory(backendTarget);

                // 2. Copiar archivos de Frontend (dist)
                string distPath = Path.Combine(_settings.Frontend.ProjectPath, _settings.Frontend.OutputPath);
                await Task.Run(() => CopyDirectory(distPath, frontendTarget));

                // 3. Copiar archivos de Backend
                await Task.Run(() => CopyDirectory(backendOutputFolder, backendTarget));

                // 3.1. Generar version.json tanto en Backend como en Frontend con la versión exacta del Release
                var versionPayload = new
                {
                    version = info.Version,
                    fecha = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss")
                };
                string versionJson = System.Text.Json.JsonSerializer.Serialize(versionPayload, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(Path.Combine(backendTarget, "version.json"), versionJson);
                await File.WriteAllTextAsync(Path.Combine(frontendTarget, "version.json"), versionJson);

                // 4. Crear archivo ZIP
                string zipPath = Path.Combine(_settings.Deployment.LocalReleasesPath, $"SIAPP_{info.Version}.zip");
                if (File.Exists(zipPath))
                {
                    File.Delete(zipPath);
                }

                await Task.Run(() => ZipFile.CreateFromDirectory(info.LocalReleaseFolder, zipPath, CompressionLevel.Optimal, false));

                info.LocalZipPath = zipPath;
                var zipFileInfo = new FileInfo(zipPath);

                result.Success = true;
                result.Duration = DateTime.Now - startTime;
                ConsoleHelper.PrintStepSuccess($"Paquete ZIP creado: {Path.GetFileName(zipPath)} ({ConsoleHelper.FormatBytes(zipFileInfo.Length)})");
                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = $"Error al empaquetar release: {ex.Message}";
                ConsoleHelper.PrintStepFailed(result.ErrorMessage);
                return result;
            }
        }

        private static void CopyDirectory(string sourceDir, string targetDir)
        {
            Directory.CreateDirectory(targetDir);

            foreach (string file in Directory.GetFiles(sourceDir))
            {
                string targetFile = Path.Combine(targetDir, Path.GetFileName(file));
                File.Copy(file, targetFile, true);
            }

            foreach (string subDir in Directory.GetDirectories(sourceDir))
            {
                string targetSubDir = Path.Combine(targetDir, Path.GetFileName(subDir));
                CopyDirectory(subDir, targetSubDir);
            }
        }
    }
}
