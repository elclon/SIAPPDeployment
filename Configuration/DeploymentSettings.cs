namespace SIAPPDeployment.Configuration
{
    public class DeploymentSettings
    {
        public FrontendSettings Frontend { get; set; } = new();
        public BackendSettings Backend { get; set; } = new();
        public DeploymentPathSettings Deployment { get; set; } = new();
        public ServerSettings Server { get; set; } = new();
        public HealthCheckSettings HealthCheck { get; set; } = new();
        public CleanupSettings Cleanup { get; set; } = new();
    }

    public class DeploymentPathSettings
    {
        public string LocalReleasesPath { get; set; } = "D:\\Sistemas\\SIAPP\\Releases";
    }

    public class CleanupSettings
    {
        public bool Enabled { get; set; } = true;
        public int KeepPastReleasesCount { get; set; } = 1;
        public bool CleanLocal { get; set; } = true;
        public bool CleanRemote { get; set; } = true;
    }
}
