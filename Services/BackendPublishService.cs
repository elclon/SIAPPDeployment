using System;
using System.IO;
using System.Threading.Tasks;
using SIAPPDeployment.Configuration;
using SIAPPDeployment.Models;
using SIAPPDeployment.Utilities;

namespace SIAPPDeployment.Services
{
    public interface IBackendPublishService
    {
        Task<StepResult> PublishAsync(DeploymentInfo info, string outputFolder);
    }

    public class BackendPublishService : IBackendPublishService
    {
        private readonly BackendSettings _settings;

        public BackendPublishService(DeploymentSettings settings)
        {
            _settings = settings.Backend;
        }

        public async Task<StepResult> PublishAsync(DeploymentInfo info, string outputFolder)
        {
            var startTime = DateTime.Now;
            var result = new StepResult
            {
                StepNumber = 2,
                StepName = "Publicando Backend"
            };

            if (info.IsDryRun)
            {
                ConsoleHelper.PrintStepSkipped("[SIMULACIÓN] Publicación de Backend omitida.");
                result.Success = true;
                result.Duration = TimeSpan.Zero;
                return result;
            }

            // 1. Validar directorio del proyecto Backend
            if (!Directory.Exists(_settings.ProjectPath))
            {
                result.Success = false;
                result.ErrorMessage = $"La ruta del Backend no existe: {_settings.ProjectPath}";
                ConsoleHelper.PrintStepFailed(result.ErrorMessage);
                return result;
            }

            // Asegurar que la carpeta de destino esté limpia
            if (Directory.Exists(outputFolder))
            {
                Directory.Delete(outputFolder, true);
            }
            Directory.CreateDirectory(outputFolder);

            // 2. Ejecutar dotnet publish con configuración Release y carpeta de salida
            string publishArgs = $"publish \"{_settings.ProjectPath}\" -c {_settings.Configuration} -o \"{outputFolder}\" --nologo";

            var procResult = await ProcessHelper.ExecuteAsync(
                "dotnet",
                publishArgs,
                _settings.ProjectPath,
                captureOutput: true
            );

            if (!procResult.Success)
            {
                result.Success = false;
                result.ErrorMessage = $"Fallo al publicar Backend: {procResult.StandardError}";
                ConsoleHelper.PrintStepFailed(result.ErrorMessage);
                return result;
            }

            // 3. Validar que se hayan generado los archivos principales
            string mainDll = Path.Combine(outputFolder, "SIAPPServer.dll");
            if (!File.Exists(mainDll))
            {
                result.Success = false;
                result.ErrorMessage = $"No se encontró el ensamblado principal '{mainDll}' en la carpeta de publicación.";
                ConsoleHelper.PrintStepFailed(result.ErrorMessage);
                return result;
            }

            result.Success = true;
            result.Duration = DateTime.Now - startTime;
            ConsoleHelper.PrintStepSuccess($"Backend publicado en: {outputFolder} ({result.Duration:mm\\:ss})");
            return result;
        }
    }
}
