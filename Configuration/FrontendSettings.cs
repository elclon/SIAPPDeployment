namespace SIAPPDeployment.Configuration
{
    public class FrontendSettings
    {
        public string ProjectPath { get; set; } = string.Empty;
        public string BuildCommand { get; set; } = "npm run build";
        public string OutputPath { get; set; } = "dist";
    }
}
