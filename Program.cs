using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SIAPPDeployment.Configuration;
using SIAPPDeployment.Services;
using SIAPPDeployment.Utilities;

namespace SIAPPDeployment
{
    internal class Program
    {
        static async Task<int> Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;

            bool isDryRun = args.Contains("--dry-run", StringComparer.OrdinalIgnoreCase);
            bool autoConfirm = args.Contains("--yes", StringComparer.OrdinalIgnoreCase) || args.Contains("-y", StringComparer.OrdinalIgnoreCase);

            // 1. Configuración desde appsettings.json
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            var deploymentSettings = new DeploymentSettings();
            configuration.Bind(deploymentSettings);

            if (args.Contains("--remove-offline", StringComparer.OrdinalIgnoreCase))
            {
                var srv = deploymentSettings.Server;
                string pwd = srv.Password ?? "";
                Console.WriteLine($"Conectando por SSH a {srv.Host}...");
                using var ssh = new Renci.SshNet.SshClient(srv.Host, srv.Port, srv.Username, pwd);
                ssh.Connect();
                Console.WriteLine("Eliminando app_offline.htm en " + srv.CurrentBackendPath);
                var cmd = ssh.RunCommand($"powershell -NoProfile -Command \"if (Test-Path '{srv.CurrentBackendPath}\\app_offline.htm') {{ Remove-Item '{srv.CurrentBackendPath}\\app_offline.htm' -Force; Write-Output 'ELIMINADO' }} else {{ Write-Output 'NO EXISTE' }}\"");
                Console.WriteLine("Resultado: " + cmd.Result);
                ssh.Disconnect();
                return 0;
            }

            // 2. Inyección de Dependencias
            var services = new ServiceCollection();
            services.AddSingleton(deploymentSettings);
            services.AddSingleton<IFrontendBuildService, FrontendBuildService>();
            services.AddSingleton<IBackendPublishService, BackendPublishService>();
            services.AddSingleton<IPackageService, PackageService>();
            services.AddSingleton<ISftpService, SftpService>();
            services.AddSingleton<IReleaseActivationService, ReleaseActivationService>();
            services.AddSingleton<IHealthCheckService, HealthCheckService>();
            services.AddSingleton<ICleanupService, CleanupService>();
            services.AddSingleton<IDeploymentOrchestrator, DeploymentOrchestrator>();

            var serviceProvider = services.BuildServiceProvider();

            // 3. Obtener contraseña configurada o solicitarla si no existe
            string? password = deploymentSettings.Server.Password;
            if (!isDryRun && string.IsNullOrWhiteSpace(password))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write($"Ingrese la contraseña para el usuario SFTP/SSH [{deploymentSettings.Server.Username}@{deploymentSettings.Server.Host}]: ");
                Console.ResetColor();
                password = ReadPasswordMasked();
                Console.WriteLine();
            }

            // 4. Ejecutar el orquestador
            var orchestrator = serviceProvider.GetRequiredService<IDeploymentOrchestrator>();
            var result = await orchestrator.RunDeploymentAsync(isDryRun, autoConfirm, password);

            return result.Success ? 0 : 1;
        }

        private static string ReadPasswordMasked()
        {
            var sb = new StringBuilder();
            while (true)
            {
                var key = Console.ReadKey(intercept: true);
                if (key.Key == ConsoleKey.Enter)
                {
                    Console.WriteLine();
                    break;
                }
                if (key.Key == ConsoleKey.Backspace)
                {
                    if (sb.Length > 0)
                    {
                        sb.Remove(sb.Length - 1, 1);
                        Console.Write("\b \b");
                    }
                }
                else if (!char.IsControl(key.KeyChar))
                {
                    sb.Append(key.KeyChar);
                    Console.Write("*");
                }
            }
            return sb.ToString();
        }
    }
}
