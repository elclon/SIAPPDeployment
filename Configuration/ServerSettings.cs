namespace SIAPPDeployment.Configuration
{
    public class ServerSettings
    {
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; } = 22;
        public string Username { get; set; } = "Administrador";
        public string? Password { get; set; }
        public string TempPath { get; set; } = "C:\\SIAPP\\temp";
        public string ReleasesPath { get; set; } = "C:\\SIAPP\\releases";
        public string CurrentFrontendPath { get; set; } = string.Empty;
        public string CurrentBackendPath { get; set; } = string.Empty;
        public bool AppOfflineEnabled { get; set; } = true;
    }
}
