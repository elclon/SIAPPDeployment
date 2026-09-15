using System;

namespace SIAPPDeployment.Utilities
{
    public static class ConsoleHelper
    {
        public static void PrintHeader(string title)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("=================================================");
            Console.WriteLine($" {title}");
            Console.WriteLine("=================================================");
            Console.ResetColor();
            Console.WriteLine();
        }

        public static void PrintSection(string section)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"--- {section} ---");
            Console.ResetColor();
        }

        public static void PrintStepStart(int current, int total, string stepName)
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write($"[{current}/{total}] {stepName.PadRight(32)}");
            Console.ResetColor();
        }

        public static void PrintStepSuccess(string? message = null)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("[ OK ]");
            Console.ResetColor();
            if (!string.IsNullOrEmpty(message))
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"      ↳ {message}");
                Console.ResetColor();
            }
        }

        public static void PrintStepFailed(string error)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("[ ERROR ]");
            Console.ResetColor();
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"      ✗ {error}");
            Console.ResetColor();
        }

        public static void PrintStepSkipped(string reason)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("[ OMITIDO ]");
            Console.ResetColor();
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"      ↳ {reason}");
            Console.ResetColor();
        }

        public static void PrintProgressBar(long current, long total, int barWidth = 30)
        {
            if (total <= 0) return;
            double percentage = (double)current / total;
            int filled = (int)(percentage * barWidth);
            int empty = barWidth - filled;

            string bar = new string('█', filled) + new string('░', Math.Max(0, empty));
            double percentVal = percentage * 100;

            Console.Write($"\r      {bar} {percentVal:0.0}% ({FormatBytes(current)}/{FormatBytes(total)})");
            if (current >= total)
            {
                Console.WriteLine();
            }
        }

        public static bool AskConfirmation(string prompt)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write($"{prompt} [S/N]: ");
            Console.ResetColor();
            var key = Console.ReadKey();
            Console.WriteLine();
            return key.Key == ConsoleKey.S;
        }

        public static void PrintSuccessSummary(string version, TimeSpan totalDuration)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("=================================================");
            Console.WriteLine(" SIAPP DESPLEGADO EXITOSAMENTE");
            Console.WriteLine("=================================================");
            Console.ResetColor();
            Console.WriteLine($" Versión desplegada: {version}");
            Console.WriteLine($" Tiempo total:       {totalDuration:hh\\:mm\\:ss}");
            Console.WriteLine("=================================================");
            Console.WriteLine();
        }

        public static void PrintErrorSummary(string failedStep, string errorMessage)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("=================================================");
            Console.WriteLine(" DESPLIEGUE DETENIDO POR ERROR");
            Console.WriteLine("=================================================");
            Console.ResetColor();
            Console.WriteLine($" Etapa fallida: {failedStep}");
            Console.WriteLine($" Detalle:       {errorMessage}");
            Console.WriteLine("=================================================");
            Console.WriteLine();
        }

        public static string FormatBytes(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB" };
            int i = 0;
            double dBytes = bytes;
            while (dBytes >= 1024 && i < suffixes.Length - 1)
            {
                dBytes /= 1024;
                i++;
            }
            return $"{dBytes:0.##} {suffixes[i]}";
        }
    }
}
