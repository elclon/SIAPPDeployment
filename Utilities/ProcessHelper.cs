using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace SIAPPDeployment.Utilities
{
    public class ProcessExecutionResult
    {
        public int ExitCode { get; set; }
        public bool Success => ExitCode == 0;
        public string StandardOutput { get; set; } = string.Empty;
        public string StandardError { get; set; } = string.Empty;
    }

    public static class ProcessHelper
    {
        public static async Task<ProcessExecutionResult> ExecuteAsync(
            string fileName,
            string arguments,
            string workingDirectory,
            bool captureOutput = true,
            Action<string>? onOutputLine = null,
            Action<string>? onErrorLine = null)
        {
            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = captureOutput,
                RedirectStandardError = captureOutput,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };

            if (captureOutput)
            {
                process.OutputDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        outputBuilder.AppendLine(e.Data);
                        onOutputLine?.Invoke(e.Data);
                    }
                };

                process.ErrorDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        errorBuilder.AppendLine(e.Data);
                        onErrorLine?.Invoke(e.Data);
                    }
                };
            }

            try
            {
                process.Start();

                if (captureOutput)
                {
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                }

                await process.WaitForExitAsync();

                return new ProcessExecutionResult
                {
                    ExitCode = process.ExitCode,
                    StandardOutput = outputBuilder.ToString(),
                    StandardError = errorBuilder.ToString()
                };
            }
            catch (Exception ex)
            {
                return new ProcessExecutionResult
                {
                    ExitCode = -1,
                    StandardError = $"Excepción al ejecutar el proceso '{fileName}': {ex.Message}"
                };
            }
        }

        public static async Task<ProcessExecutionResult> ExecuteShellCommandAsync(
            string command,
            string workingDirectory,
            bool captureOutput = true,
            Action<string>? onOutputLine = null)
        {
            return await ExecuteAsync("cmd.exe", $"/c {command}", workingDirectory, captureOutput, onOutputLine);
        }
    }
}
