using System;
using System.IO;
using System.Threading.Tasks;
using Renci.SshNet;
using SIAPPDeployment.Configuration;
using SIAPPDeployment.Models;
using SIAPPDeployment.Utilities;

namespace SIAPPDeployment.Services
{
    public interface ISftpService
    {
        Task<StepResult> UploadPackageAsync(DeploymentInfo info, string password);
    }

    public class SftpService : ISftpService
    {
        private readonly ServerSettings _settings;

        public SftpService(DeploymentSettings settings)
        {
            _settings = settings.Server;
        }

        public async Task<StepResult> UploadPackageAsync(DeploymentInfo info, string password)
        {
            var startTime = DateTime.Now;
            var result = new StepResult
            {
                StepNumber = 4,
                StepName = "Subiendo al servidor"
            };

            if (info.IsDryRun)
            {
                ConsoleHelper.PrintStepSkipped("[SIMULACI�N] Subida SFTP omitida.");
                result.Success = true;
                result.Duration = TimeSpan.Zero;
                return result;
            }

            if (!File.Exists(info.LocalZipPath))
            {
                result.Success = false;
                result.ErrorMessage = "El archivo ZIP local no existe: " + info.LocalZipPath;
                ConsoleHelper.PrintStepFailed(result.ErrorMessage);
                return result;
            }

            try
            {
                string fileName = Path.GetFileName(info.LocalZipPath);
                string winTempDir = _settings.TempPath.Replace("/", "\\");
                string remoteFilePath = Path.Combine(winTempDir, fileName);
                info.RemoteZipPath = remoteFilePath;

                var connectionInfo = new ConnectionInfo(
                    _settings.Host,
                    _settings.Port,
                    _settings.Username,
                    new PasswordAuthenticationMethod(_settings.Username, password)
                );

                // 1. Asegurar directorios en Windows Server v�a SSH
                using (var ssh = new SshClient(connectionInfo))
                {
                    await Task.Run(() => ssh.Connect());
                    string mkdirCmdText = "powershell -NoProfile -Command \"New-Item -ItemType Directory -Force -Path '" + winTempDir + "', '" + _settings.ReleasesPath + "'\"";
                    var mkdirCmd = ssh.CreateCommand(mkdirCmdText);
                    mkdirCmd.Execute();
                    ssh.Disconnect();
                }

                // 2. Transferencia usando ScpClient (altamente compatible con Windows OpenSSH)
                bool uploadSuccess = false;

                try
                {
                    using var scp = new ScpClient(connectionInfo);
                    await Task.Run(() => scp.Connect());

                    var fileInfo = new FileInfo(info.LocalZipPath);
                    long totalBytes = fileInfo.Length;

                    scp.Uploading += (sender, e) =>
                    {
                        ConsoleHelper.PrintProgressBar(e.Uploaded, totalBytes);
                    };

                    string scpDestination = remoteFilePath.Replace("\\", "/");
                    await Task.Run(() => scp.Upload(fileInfo, scpDestination));
                    scp.Disconnect();
                    uploadSuccess = true;
                }
                catch
                {
                    // Fallback a SFTP
                }

                // Fallback: Si SCP no estuviera habilitado, intentar SFTP por defecto
                if (!uploadSuccess)
                {
                    using var sftp = new SftpClient(connectionInfo);
                    await Task.Run(() => sftp.Connect());

                    using var fileStream = File.OpenRead(info.LocalZipPath);
                    long totalBytes = fileStream.Length;

                    string sftpPath = remoteFilePath.StartsWith("C:", StringComparison.OrdinalIgnoreCase)
                        ? "/" + remoteFilePath.Replace("\\", "/")
                        : remoteFilePath.Replace("\\", "/");

                    await Task.Run(() =>
                    {
                        sftp.UploadFile(fileStream, sftpPath, (uploaded) =>
                        {
                            ConsoleHelper.PrintProgressBar((long)uploaded, totalBytes);
                        });
                    });

                    sftp.Disconnect();
                }

                result.Success = true;
                result.Duration = DateTime.Now - startTime;
                ConsoleHelper.PrintStepSuccess($"Archivo transferido a: {remoteFilePath} ({result.Duration:mm\\:ss})");
                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = "Error en la transferencia SFTP/SCP: " + ex.Message;
                ConsoleHelper.PrintStepFailed(result.ErrorMessage);
                return result;
            }
        }
    }
}
