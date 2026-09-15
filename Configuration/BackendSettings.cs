namespace SIAPPDeployment.Configuration
{
    public class BackendSettings
    {
        public string ProjectPath { get; set; } = string.Empty;
        public string PublishCommand { get; set; } = "dotnet publish";
        public string Configuration { get; set; } = "Release";
        public string PublishProfile { get; set; } = "Api";
    }
}
