using System;
using System.Collections.Generic;

namespace UTF.Core
{
    /// <summary>
    /// 测试计划信息
    /// </summary>
    public class TestPlanInfo
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public int EstimatedDurationMinutes { get; set; }
        public bool AutoRun { get; set; }
        public bool GenerateReport { get; set; } = true;
        public List<TestPlanStepInfo> TestSteps { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }
        public string Version { get; set; } = "1.0";
        public string Author { get; set; } = "";
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    /// <summary>
    /// 测试计划步骤信息
    /// </summary>
    public class TestPlanStepInfo
    {
        public string StepName { get; set; } = "";
        public string Description { get; set; } = "";
        public string StepType { get; set; } = "";
        public int TimeoutSeconds { get; set; } = 60;
        public bool IsEnabled { get; set; } = true;
        public bool IsCritical { get; set; } = false;
        public int RetryCount { get; set; } = 0;
        public Dictionary<string, object> Parameters { get; set; } = new();
        public string ExpectedResult { get; set; } = "";
        public List<string> Tags { get; set; } = new();
    }

    /// <summary>
    /// 测试计划执行配置
    /// </summary>
    public class TestPlanExecutionConfig
    {
        public bool StopOnFirstFailure { get; set; } = false;
        public bool GenerateDetailedLogs { get; set; } = true;
        public bool EnableScreenshots { get; set; } = false;
        public int MaxParallelDUTs { get; set; } = 10;
        public TimeSpan GlobalTimeout { get; set; } = TimeSpan.FromMinutes(60);
        public string ReportTemplate { get; set; } = "default";
        public Dictionary<string, object> GlobalParameters { get; set; } = new();
    }
}
