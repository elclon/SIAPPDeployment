using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using SIAPPDeployment.Configuration;
using SIAPPDeployment.Models;
using SIAPPDeployment.Utilities;

namespace SIAPPDeployment.Services
{
    public interface IDeploymentOrchestrator
    {
        Task<DeploymentResult> RunDeploymentAsync(bool isDryRun, bool autoConfirm, string? password);
    }

    public class DeploymentOrchestrator : IDeploymentOrchestrator
    {
        private readonly DeploymentSettings _settings;
        private readonly IFrontendBuildService _frontendService;
        private readonly IBackendPublishService _backendService;
        private readonly IPackageService _packageService;
        private readonly ISftpService _sftpService;
        private readonly IReleaseActivationService _activationService;
        private readonly IHealthCheckService _healthCheckService;
        private readonly ICleanupService _cleanupService;

        public DeploymentOrchestrator(
            DeploymentSettings settings,
            IFrontendBuildService frontendService,
            IBackendPublishService backendService,
            IPackageService packageService,
            ISftpService sftpService,
            IReleaseActivationService activationService,
            IHealthCheckService healthCheckService,
            ICleanupService cleanupService)
        {
            _settings = settings;
            _frontendService = frontendService;
            _backendService = backendService;
            _packageService = packageService;
            _sftpService = sftpService;
            _activationService = activationService;
            _healthCheckService = healthCheckService;
            _cleanupService = cleanupService;
        }

        public async Task<DeploymentResult> RunDeploymentAsync(bool isDryRun, bool autoConfirm, string? password)
        {
            var overallStart = DateTime.Now;
            string version = _packageService.GenerateVersionIdentifier();

            var info = new DeploymentInfo
            {
                Version = version,
                IsDryRun = isDryRun
            };

            var result = new DeploymentResult
            {
                Version = version,
                Success = false
            };

            ConsoleHelper.PrintHeader($"DESPLIEGUE DE SIAPP {(isDryRun ? "[MODO SIMULACIÓN]" : "")}");
            Console.WriteLine($" Versión asignada: {version}");
            Console.WriteLine($" Servidor destino:  {_settings.Server.Host}");
            Console.WriteLine($" API URL:          {_settings.HealthCheck.ApiUrl}");
            Console.WriteLine($" Frontend URL:     {_settings.HealthCheck.FrontendUrl}");
            Console.WriteLine();

            // Confirmación antes de continuar
            if (!autoConfirm && !isDryRun)
            {
                if (!ConsoleHelper.AskConfirmation("¿Desea iniciar el proceso de despliegue?"))
                {
                    Console.WriteLine("\nDespliegue cancelado por el usuario.");
                    result.ErrorMessage = "Cancelado por el usuario.";
                    return result;
                }
                Console.WriteLine();
            }

            string backendLocalOutput = Path.Combine(Path.GetTempPath(), "SIAPP_Backend_Publish_" + version);

            try
            {
                // [1/8] Compilando Frontend
                ConsoleHelper.PrintStepStart(1, 8, "Compilando Frontend...");
                var step1 = await _frontendService.BuildAsync(info);
                result.Steps.Add(step1);
                if (!step1.Success) return Fail(result, step1, overallStart);

                // [2/8] Publicando Backend
                ConsoleHelper.PrintStepStart(2, 8, "Publicando Backend...");
                var step2 = await _backendService.PublishAsync(info, backendLocalOutput);
                result.Steps.Add(step2);
                if (!step2.Success) return Fail(result, step2, overallStart);

                // [3/8] Preparando archivos
                ConsoleHelper.PrintStepStart(3, 8, "Preparando archivos...");
                var step3 = await _packageService.PreparePackageAsync(info, backendLocalOutput);
                result.Steps.Add(step3);
                if (!step3.Success) return Fail(result, step3, overallStart);

                // [4/8] Subiendo al servidor
                ConsoleHelper.PrintStepStart(4, 8, "Subiendo al servidor...");
                var step4 = await _sftpService.UploadPackageAsync(info, password ?? "");
                result.Steps.Add(step4);
                if (!step4.Success) return Fail(result, step4, overallStart);

                // [5/8] Activando nueva versión
                ConsoleHelper.PrintStepStart(5, 8, "Activando nueva versión...");
                var step5 = await _activationService.ActivateReleaseAsync(info, password ?? "");
                result.Steps.Add(step5);
                if (!step5.Success) return Fail(result, step5, overallStart);

                // [6/8] Verificando API
                ConsoleHelper.PrintStepStart(6, 8, "Verificando API...");
                var step6 = await _healthCheckService.CheckApiAsync(info);
                result.Steps.Add(step6);
                if (!step6.Success)
                {
                    // Preguntar si desea Rollback
                    if (!isDryRun && ConsoleHelper.AskConfirmation("El Health Check de la API falló. ¿Desea restaurar la versión previa (Rollback)?"))
                    {
                        Console.WriteLine("Iniciando Rollback...");
                        // Rollback logic
                    }
                    return Fail(result, step6, overallStart);
                }

                // [7/8] Verificando Frontend
                ConsoleHelper.PrintStepStart(7, 8, "Verificando Frontend...");
                var step7 = await _healthCheckService.CheckFrontendAsync(info);
                result.Steps.Add(step7);
                if (!step7.Success) return Fail(result, step7, overallStart);

                // [8/8] Limpiando versiones pasadas (servidor y local)
                ConsoleHelper.PrintStepStart(8, 8, "Limpiando versiones pasadas...");
                var step8 = await _cleanupService.CleanupReleasesAsync(info, password ?? "");
                result.Steps.Add(step8);
                if (!step8.Success) return Fail(result, step8, overallStart);

                result.Success = true;
                result.Duration = DateTime.Now - overallStart;
                ConsoleHelper.PrintSuccessSummary(version, result.Duration);
            }
            finally
            {
                // Limpiar archivos temporales locales
                if (Directory.Exists(backendLocalOutput))
                {
                    try { Directory.Delete(backendLocalOutput, true); } catch { }
                }

                // Guardar Log
                SaveLogFile(result, info, overallStart);
            }

            return result;
        }

        private static DeploymentResult Fail(DeploymentResult result, StepResult failedStep, DateTime start)
        {
            result.Success = false;
            result.FailedStep = failedStep.StepName;
            result.ErrorMessage = failedStep.ErrorMessage;
            result.Duration = DateTime.Now - start;
            ConsoleHelper.PrintErrorSummary(failedStep.StepName, failedStep.ErrorMessage);
            return result;
        }

        private static void SaveLogFile(DeploymentResult result, DeploymentInfo info, DateTime start)
        {
            try
            {
                string logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
                Directory.CreateDirectory(logDir);

                string logFile = Path.Combine(logDir, $"deployment_{info.Version.Replace(".", "_")}_{start:HHmmss}.log");
                var sb = new StringBuilder();
                sb.AppendLine($"=== SIAPP DEPLOYMENT LOG ===");
                sb.AppendLine($"Versión:   {info.Version}");
                sb.AppendLine($"Fecha:     {start:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"Duración:  {result.Duration}");
                sb.AppendLine($"Resultado: {(result.Success ? "EXITOSO" : "FALLIDO")}");
                if (!result.Success)
                {
                    sb.AppendLine($"Etapa:     {result.FailedStep}");
                    sb.AppendLine($"Error:     {result.ErrorMessage}");
                }
                sb.AppendLine("\n--- PASOS EJECUTADOS ---");
                foreach (var step in result.Steps)
                {
                    sb.AppendLine($"[{step.StepNumber}] {step.StepName}: {(step.Success ? "OK" : "FALLÓ")} ({step.Duration:mm\\:ss})");
                    if (!step.Success) sb.AppendLine($"    Error: {step.ErrorMessage}");
                }

                File.WriteAllText(logFile, sb.ToString());
            }
            catch { }
        }
    }
}
