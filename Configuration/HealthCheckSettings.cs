namespace SIAPPDeployment.Configuration
{
    public class HealthCheckSettings
    {
        public string ApiUrl { get; set; } = string.Empty;
        public string FrontendUrl { get; set; } = string.Empty;
        public int TimeoutSeconds { get; set; } = 30;
    }
}
