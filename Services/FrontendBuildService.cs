using System;
using System.IO;
using System.Threading.Tasks;
using SIAPPDeployment.Configuration;
using SIAPPDeployment.Models;
using SIAPPDeployment.Utilities;

namespace SIAPPDeployment.Services
{
    public interface IFrontendBuildService
    {
        Task<StepResult> BuildAsync(DeploymentInfo info);
    }

    public class FrontendBuildService : IFrontendBuildService
    {
        private readonly FrontendSettings _settings;

        public FrontendBuildService(DeploymentSettings settings)
        {
            _settings = settings.Frontend;
        }

        public async Task<StepResult> BuildAsync(DeploymentInfo info)
        {
            var startTime = DateTime.Now;
            var result = new StepResult
            {
                StepNumber = 1,
                StepName = "Compilando Frontend"
            };

            if (info.IsDryRun)
            {
                ConsoleHelper.PrintStepSkipped("[SIMULACIÓN] Compilación de Frontend omitida.");
                result.Success = true;
                result.Duration = TimeSpan.Zero;
                return result;
            }

            // 1. Validar directorio del proyecto Frontend
            if (!Directory.Exists(_settings.ProjectPath))
            {
                result.Success = false;
                result.ErrorMessage = $"La ruta del Frontend no existe: {_settings.ProjectPath}";
                ConsoleHelper.PrintStepFailed(result.ErrorMessage);
                return result;
            }

            // 2. Ejecutar comando de compilación (npm run build)
            var procResult = await ProcessHelper.ExecuteShellCommandAsync(
                _settings.BuildCommand,
                _settings.ProjectPath,
                captureOutput: true
            );

            if (!procResult.Success)
            {
                result.Success = false;
                result.ErrorMessage = $"Fallo al compilar Frontend (código {procResult.ExitCode}): {procResult.StandardError}";
                ConsoleHelper.PrintStepFailed(result.ErrorMessage);
                return result;
            }

            // 3. Validar que la carpeta de salida (dist) exista y contenga index.html
            string distPath = Path.Combine(_settings.ProjectPath, _settings.OutputPath);
            string indexPath = Path.Combine(distPath, "index.html");

            if (!Directory.Exists(distPath) || !File.Exists(indexPath))
            {
                result.Success = false;
                result.ErrorMessage = $"La carpeta de salida '{distPath}' o 'index.html' no fueron generados.";
                ConsoleHelper.PrintStepFailed(result.ErrorMessage);
                return result;
            }

            result.Success = true;
            result.Duration = DateTime.Now - startTime;
            ConsoleHelper.PrintStepSuccess($"Build generado en: {distPath} ({result.Duration:mm\\:ss})");
            return result;
        }
    }
}
