using System;
using System.Collections.Generic;

namespace SIAPPDeployment.Models
{
    public class DeploymentResult
    {
        public bool Success { get; set; }
        public string Version { get; set; } = string.Empty;
        public TimeSpan Duration { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public string FailedStep { get; set; } = string.Empty;
        public List<StepResult> Steps { get; set; } = new();
    }

    public class StepResult
    {
        public int StepNumber { get; set; }
        public string StepName { get; set; } = string.Empty;
        public bool Success { get; set; }
        public TimeSpan Duration { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }
}
