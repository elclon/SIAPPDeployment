using System;
using System.Net.Http;
using System.Threading.Tasks;
using SIAPPDeployment.Configuration;
using SIAPPDeployment.Models;
using SIAPPDeployment.Utilities;

namespace SIAPPDeployment.Services
{
    public interface IHealthCheckService
    {
        Task<StepResult> CheckApiAsync(DeploymentInfo info);
        Task<StepResult> CheckFrontendAsync(DeploymentInfo info);
    }

    public class HealthCheckService : IHealthCheckService
    {
        private readonly HealthCheckSettings _settings;
        private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(30) };

        public HealthCheckService(DeploymentSettings settings)
        {
            _settings = settings.HealthCheck;
        }

        public async Task<StepResult> CheckApiAsync(DeploymentInfo info)
        {
            var startTime = DateTime.Now;
            var result = new StepResult
            {
                StepNumber = 6,
                StepName = "Verificando API"
            };

            if (info.IsDryRun)
            {
                ConsoleHelper.PrintStepSkipped("[SIMULACIÓN] Health Check API omitido.");
                result.Success = true;
                result.Duration = TimeSpan.Zero;
                return result;
            }

            if (string.IsNullOrEmpty(_settings.ApiUrl))
            {
                ConsoleHelper.PrintStepSkipped("URL de API no configurada.");
                result.Success = true;
                return result;
            }

            try
            {
                // Reintentos automáticos para dar tiempo a que IIS cargue el Application Pool
                int maxRetries = 5;
                for (int attempt = 1; attempt <= maxRetries; attempt++)
                {
                    try
                    {
                        var response = await _httpClient.GetAsync(_settings.ApiUrl);
                        if (response.IsSuccessStatusCode)
                        {
                            result.Success = true;
                            result.Duration = DateTime.Now - startTime;
                            ConsoleHelper.PrintStepSuccess($"API respondió HTTP {(int)response.StatusCode} OK ({result.Duration:ss}s)");
                            return result;
                        }
                    }
                    catch
                    {
                        if (attempt == maxRetries) throw;
                        await Task.Delay(3000);
                    }
                }

                result.Success = false;
                result.ErrorMessage = $"La API no respondió satisfactoriamente en {_settings.ApiUrl}";
                ConsoleHelper.PrintStepFailed(result.ErrorMessage);
                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = $"Fallo al conectar con la API ({_settings.ApiUrl}): {ex.Message}";
                ConsoleHelper.PrintStepFailed(result.ErrorMessage);
                return result;
            }
        }

        public async Task<StepResult> CheckFrontendAsync(DeploymentInfo info)
        {
            var startTime = DateTime.Now;
            var result = new StepResult
            {
                StepNumber = 7,
                StepName = "Verificando Frontend"
            };

            if (info.IsDryRun)
            {
                ConsoleHelper.PrintStepSkipped("[SIMULACIÓN] Health Check Frontend omitido.");
                result.Success = true;
                result.Duration = TimeSpan.Zero;
                return result;
            }

            if (string.IsNullOrEmpty(_settings.FrontendUrl))
            {
                ConsoleHelper.PrintStepSkipped("URL de Frontend no configurada.");
                result.Success = true;
                return result;
            }

            try
            {
                var response = await _httpClient.GetAsync(_settings.FrontendUrl);
                if (response.IsSuccessStatusCode)
                {
                    string content = await response.Content.ReadAsStringAsync();
                    if (!string.IsNullOrEmpty(content) && content.Contains("<html", StringComparison.OrdinalIgnoreCase))
                    {
                        result.Success = true;
                        result.Duration = DateTime.Now - startTime;
                        ConsoleHelper.PrintStepSuccess($"Frontend respondió HTTP {(int)response.StatusCode} con HTML válido");
                        return result;
                    }
                }

                result.Success = false;
                result.ErrorMessage = $"El Frontend respondió con código {(int)response.StatusCode} o contenido no válido.";
                ConsoleHelper.PrintStepFailed(result.ErrorMessage);
                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = $"Fallo al conectar con el Frontend ({_settings.FrontendUrl}): {ex.Message}";
                ConsoleHelper.PrintStepFailed(result.ErrorMessage);
                return result;
            }
        }
    }
}
