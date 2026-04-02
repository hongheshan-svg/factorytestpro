using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace UTF.Core;

/// <summary>
/// 测试状态枚举
/// </summary>
public enum TestStatus
{
    /// <summary>未开始</summary>
    NotStarted,
    /// <summary>准备中</summary>
    Preparing,
    /// <summary>运行中</summary>
    Running,
    /// <summary>暂停中</summary>
    Paused,
    /// <summary>停止中</summary>
    Stopping,
    /// <summary>已完成</summary>
    Completed,
    /// <summary>已中止</summary>
    Aborted,
    /// <summary>发生错误</summary>
    Error
}

/// <summary>
/// 测试优先级枚举
/// </summary>
public enum TestPriority
{
    /// <summary>低优先级</summary>
    Low,
    /// <summary>普通优先级</summary>
    Normal,
    /// <summary>高优先级</summary>
    High,
    /// <summary>紧急优先级</summary>
    Critical
}

/// <summary>
/// 测试步骤
/// </summary>
public sealed record TestStep
{
    /// <summary>步骤ID</summary>
    public string StepId { get; init; } = string.Empty;
    
    /// <summary>步骤名称</summary>
    public string Name { get; init; } = string.Empty;
    
    /// <summary>步骤描述</summary>
    public string Description { get; init; } = string.Empty;
    
    /// <summary>步骤类型</summary>
    public string StepType { get; init; } = string.Empty;
    
    /// <summary>目标设备ID</summary>
    public string? TargetDeviceId { get; init; }
    
    /// <summary>操作命令</summary>
    public string Command { get; init; } = string.Empty;
    
    /// <summary>参数</summary>
    public Dictionary<string, object> Parameters { get; init; } = new();
    
    /// <summary>期望结果</summary>
    public object? ExpectedResult { get; init; }
    
    /// <summary>最小值</summary>
    public object? MinValue { get; init; }
    
    /// <summary>最大值</summary>
    public object? MaxValue { get; init; }
    
    /// <summary>单位</summary>
    public string Unit { get; init; } = string.Empty;
    
    /// <summary>超时时间</summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);
    
    /// <summary>重试次数</summary>
    public int RetryCount { get; init; } = 0;
    
    /// <summary>是否关键步骤</summary>
    public bool IsCritical { get; init; } = true;
    
    /// <summary>前置条件</summary>
    public List<string> Prerequisites { get; init; } = new();
    
    /// <summary>清理操作</summary>
    public List<string> CleanupActions { get; init; } = new();
    
    /// <summary>变量存储名称</summary>
    public string? StoreResultAs { get; init; }
}

/// <summary>
/// 测试步骤执行结果
/// </summary>
public sealed record TestStepExecutionResult
{
    /// <summary>步骤ID</summary>
    public string StepId { get; init; } = string.Empty;
    
    /// <summary>步骤名称</summary>
    public string StepName { get; init; } = string.Empty;
    
    /// <summary>是否通过</summary>
    public bool Passed { get; init; }
    
    /// <summary>测量值</summary>
    public object? MeasuredValue { get; init; }
    
    /// <summary>期望值</summary>
    public object? ExpectedValue { get; init; }
    
    /// <summary>错误信息</summary>
    public string? ErrorMessage { get; init; }
    
    /// <summary>开始时间</summary>
    public DateTime StartTime { get; init; }
    
    /// <summary>结束时间</summary>
    public DateTime EndTime { get; init; }
    
    /// <summary>执行时间</summary>
    public TimeSpan ExecutionTime => EndTime - StartTime;
    
    /// <summary>重试次数</summary>
    public int RetryCount { get; init; }
    
    /// <summary>扩展数据</summary>
    public Dictionary<string, object> ExtendedData { get; init; } = new();
}

/// <summary>
/// 测试序列
/// </summary>
public sealed record TestSequence
{
    /// <summary>序列ID</summary>
    public string SequenceId { get; init; } = string.Empty;
    
    /// <summary>序列名称</summary>
    public string Name { get; init; } = string.Empty;
    
    /// <summary>序列描述</summary>
    public string Description { get; init; } = string.Empty;
    
    /// <summary>版本号</summary>
    public string Version { get; init; } = "1.0";
    
    /// <summary>测试步骤列表</summary>
    public List<TestStep> Steps { get; init; } = new();
    
    /// <summary>所需设备列表</summary>
    public List<string> RequiredDevices { get; init; } = new();
    
    /// <summary>预计执行时间</summary>
    public TimeSpan EstimatedDuration { get; init; }
    
    /// <summary>优先级</summary>
    public TestPriority Priority { get; init; } = TestPriority.Normal;
    
    /// <summary>并行执行</summary>
    public bool AllowParallelExecution { get; init; } = false;
    
    /// <summary>失败时停止</summary>
    public bool StopOnFailure { get; init; } = true;
    
    /// <summary>标签</summary>
    public List<string> Tags { get; init; } = new();
    
    /// <summary>扩展属性</summary>
    public Dictionary<string, object> Properties { get; init; } = new();
}

/// <summary>
/// 测试计划
/// </summary>
public sealed record TestPlan
{
    /// <summary>计划ID</summary>
    public string PlanId { get; init; } = string.Empty;
    
    /// <summary>计划名称</summary>
    public string Name { get; init; } = string.Empty;
    
    /// <summary>计划描述</summary>
    public string Description { get; init; } = string.Empty;
    
    /// <summary>版本号</summary>
    public string Version { get; init; } = "1.0";
    
    /// <summary>测试序列列表</summary>
    public List<TestSequence> Sequences { get; init; } = new();
    
    /// <summary>目标DUT类型</summary>
    public string TargetDUTType { get; init; } = string.Empty;
    
    /// <summary>创建时间</summary>
    public DateTime CreatedTime { get; init; } = DateTime.UtcNow;
    
    /// <summary>创建者</summary>
    public string CreatedBy { get; init; } = string.Empty;
    
    /// <summary>最大并行DUT数量</summary>
    public int MaxParallelDUTs { get; init; } = 1;
    
    /// <summary>全局变量</summary>
    public Dictionary<string, object> GlobalVariables { get; init; } = new();
}

/// <summary>
/// 测试任务
/// </summary>
public sealed record TestTask
{
    /// <summary>任务ID</summary>
    public string TaskId { get; init; } = string.Empty;
    
    /// <summary>任务名称</summary>
    public string Name { get; init; } = string.Empty;
    
    /// <summary>测试计划</summary>
    public TestPlan TestPlan { get; init; } = new();
    
    /// <summary>目标DUT ID列表</summary>
    public List<string> TargetDUTIds { get; init; } = new();
    
    /// <summary>任务状态</summary>
    public TestStatus Status { get; init; } = TestStatus.NotStarted;
    
    /// <summary>优先级</summary>
    public TestPriority Priority { get; init; } = TestPriority.Normal;
    
    /// <summary>创建时间</summary>
    public DateTime CreatedTime { get; init; } = DateTime.UtcNow;
    
    /// <summary>开始时间</summary>
    public DateTime? StartTime { get; init; }
    
    /// <summary>结束时间</summary>
    public DateTime? EndTime { get; init; }
    
    /// <summary>操作员</summary>
    public string Operator { get; init; } = string.Empty;
    
    /// <summary>测试站台</summary>
    public string TestStation { get; init; } = string.Empty;
    
    /// <summary>任务变量</summary>
    public Dictionary<string, object> Variables { get; init; } = new();
}

/// <summary>
/// 测试结果报告
/// </summary>
public sealed record TestReport
{
    /// <summary>报告ID</summary>
    public string ReportId { get; init; } = string.Empty;
    
    /// <summary>任务ID</summary>
    public string TaskId { get; init; } = string.Empty;
    
    /// <summary>DUT ID</summary>
    public string DUTId { get; init; } = string.Empty;
    
    /// <summary>总体结果</summary>
    public bool OverallResult { get; init; }
    
    /// <summary>步骤结果列表</summary>
    public List<TestStepExecutionResult> StepResults { get; init; } = new();
    
    /// <summary>开始时间</summary>
    public DateTime StartTime { get; init; }
    
    /// <summary>结束时间</summary>
    public DateTime EndTime { get; init; }
    
    /// <summary>总执行时间</summary>
    public TimeSpan TotalExecutionTime => EndTime - StartTime;
    
    /// <summary>操作员</summary>
    public string Operator { get; init; } = string.Empty;
    
    /// <summary>测试站台</summary>
    public string TestStation { get; init; } = string.Empty;
    
    /// <summary>通过的步骤数</summary>
    public int PassedSteps => StepResults.Count(r => r.Passed);
    
    /// <summary>失败的步骤数</summary>
    public int FailedSteps => StepResults.Count(r => !r.Passed);
    
    /// <summary>总步骤数</summary>
    public int TotalSteps => StepResults.Count;
    
    /// <summary>通过率</summary>
    public double PassRate => TotalSteps > 0 ? (double)PassedSteps / TotalSteps : 0.0;
    
    /// <summary>扩展数据</summary>
    public Dictionary<string, object> ExtendedData { get; init; } = new();
}

/// <summary>
/// 测试事件参数
/// </summary>
public sealed class TestEventArgs : EventArgs
{
    public string TaskId { get; init; } = string.Empty;
    public string DUTId { get; init; } = string.Empty;
    public string EventType { get; init; } = string.Empty;
    public object? Data { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// 测试引擎接口
/// </summary>
public interface ITestEngine
{
    /// <summary>引擎状态</summary>
    TestStatus Status { get; }
    
    /// <summary>当前运行的任务列表</summary>
    IReadOnlyList<TestTask> RunningTasks { get; }
    
    /// <summary>任务状态变化事件</summary>
    event EventHandler<TestEventArgs>? TaskStatusChanged;
    
    /// <summary>步骤完成事件</summary>
    event EventHandler<TestEventArgs>? StepCompleted;
    
    /// <summary>测试完成事件</summary>
    event EventHandler<TestEventArgs>? TestCompleted;
    
    /// <summary>错误事件</summary>
    event EventHandler<TestEventArgs>? ErrorOccurred;
    
    /// <summary>初始化测试引擎</summary>
    Task<bool> InitializeAsync(CancellationToken cancellationToken = default);
    
    /// <summary>关闭测试引擎</summary>
    Task<bool> ShutdownAsync(CancellationToken cancellationToken = default);
    
    /// <summary>加载测试计划</summary>
    Task<TestPlan?> LoadTestPlanAsync(string planPath, CancellationToken cancellationToken = default);
    
    /// <summary>验证测试计划</summary>
    Task<bool> ValidateTestPlanAsync(TestPlan testPlan, CancellationToken cancellationToken = default);
    
    /// <summary>创建测试任务</summary>
    Task<TestTask?> CreateTestTaskAsync(TestPlan testPlan, List<string> dutIds, string? operatorName = null, CancellationToken cancellationToken = default);
    
    /// <summary>执行测试任务</summary>
    Task<TestReport?> ExecuteTestTaskAsync(TestTask testTask, CancellationToken cancellationToken = default);
    
    /// <summary>执行单个测试序列</summary>
    Task<List<TestStepExecutionResult>> ExecuteTestSequenceAsync(TestSequence sequence, string dutId, CancellationToken cancellationToken = default);
    
    /// <summary>执行单个测试步骤</summary>
    Task<TestStepExecutionResult> ExecuteTestStepAsync(TestStep step, string dutId, CancellationToken cancellationToken = default);
    
    /// <summary>暂停测试任务</summary>
    Task<bool> PauseTestTaskAsync(string taskId, CancellationToken cancellationToken = default);
    
    /// <summary>恢复测试任务</summary>
    Task<bool> ResumeTestTaskAsync(string taskId, CancellationToken cancellationToken = default);
    
    /// <summary>停止测试任务</summary>
    Task<bool> StopTestTaskAsync(string taskId, CancellationToken cancellationToken = default);
    
    /// <summary>获取任务状态</summary>
    Task<TestStatus> GetTaskStatusAsync(string taskId, CancellationToken cancellationToken = default);
    
    /// <summary>获取测试报告</summary>
    Task<TestReport?> GetTestReportAsync(string taskId, string dutId, CancellationToken cancellationToken = default);
    
    /// <summary>清理测试环境</summary>
    Task<bool> CleanupTestEnvironmentAsync(CancellationToken cancellationToken = default);
}
