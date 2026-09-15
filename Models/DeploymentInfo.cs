using System;

namespace SIAPPDeployment.Models
{
    public class DeploymentInfo
    {
        public string Version { get; set; } = string.Empty;
        public DateTime StartTime { get; set; } = DateTime.Now;
        public bool IsDryRun { get; set; } = false;
        public string LocalReleaseFolder { get; set; } = string.Empty;
        public string LocalZipPath { get; set; } = string.Empty;
        public string RemoteZipPath { get; set; } = string.Empty;
        public string RemoteReleaseFolder { get; set; } = string.Empty;
    }
}
