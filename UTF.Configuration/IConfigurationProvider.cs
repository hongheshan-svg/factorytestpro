using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace UTF.Configuration;

/// <summary>
/// 配置文件格式枚举
/// </summary>
public enum ConfigurationFormat
{
    /// <summary>JSON格式</summary>
    Json,
    /// <summary>XML格式</summary>
    Xml,
    /// <summary>YAML格式</summary>
    Yaml,
    /// <summary>INI格式</summary>
    Ini,
    /// <summary>Properties格式</summary>
    Properties,
    /// <summary>自定义格式</summary>
    Custom
}

/// <summary>
/// 配置验证结果
/// </summary>
public sealed record ConfigurationValidationResult
{
    /// <summary>是否有效</summary>
    public bool IsValid { get; init; }
    
    /// <summary>错误列表</summary>
    public List<string> Errors { get; init; } = new();
    
    /// <summary>警告列表</summary>
    public List<string> Warnings { get; init; } = new();
    
    /// <summary>验证的配置路径</summary>
    public string ConfigurationPath { get; init; } = string.Empty;
    
    /// <summary>验证时间</summary>
    public DateTime ValidationTime { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// 设备配置信息
/// </summary>
public sealed record DeviceConfigurationInfo
{
    /// <summary>设备ID</summary>
    public string DeviceId { get; init; } = string.Empty;
    
    /// <summary>设备名称</summary>
    public string Name { get; init; } = string.Empty;
    
    /// <summary>设备类型</summary>
    public string DeviceType { get; init; } = string.Empty;
    
    /// <summary>设备分类</summary>
    public string Category { get; init; } = string.Empty;
    
    /// <summary>制造商</summary>
    public string Manufacturer { get; init; } = string.Empty;
    
    /// <summary>型号</summary>
    public string Model { get; init; } = string.Empty;
    
    /// <summary>连接类型</summary>
    public string ConnectionType { get; init; } = string.Empty;
    
    /// <summary>连接参数</summary>
    public Dictionary<string, object> ConnectionParameters { get; init; } = new();
    
    /// <summary>初始化参数</summary>
    public Dictionary<string, object> InitializationParameters { get; init; } = new();
    
    /// <summary>校准参数</summary>
    public Dictionary<string, object> CalibrationParameters { get; init; } = new();
    
    /// <summary>是否自动连接</summary>
    public bool AutoConnect { get; init; } = true;
    
    /// <summary>超时时间(毫秒)</summary>
    public int TimeoutMs { get; init; } = 30000;
    
    /// <summary>重试次数</summary>
    public int RetryCount { get; init; } = 3;
    
    /// <summary>是否启用</summary>
    public bool Enabled { get; init; } = true;
    
    /// <summary>优先级</summary>
    public int Priority { get; init; } = 0;
    
    /// <summary>标签</summary>
    public List<string> Tags { get; init; } = new();
    
    /// <summary>扩展配置</summary>
    public Dictionary<string, object> ExtendedConfiguration { get; init; } = new();
}

/// <summary>
/// 测试序列配置信息
/// </summary>
public sealed record TestSequenceConfigurationInfo
{
    /// <summary>序列ID</summary>
    public string SequenceId { get; init; } = string.Empty;
    
    /// <summary>序列名称</summary>
    public string Name { get; init; } = string.Empty;
    
    /// <summary>序列描述</summary>
    public string Description { get; init; } = string.Empty;
    
    /// <summary>版本</summary>
    public string Version { get; init; } = "1.0";
    
    /// <summary>测试步骤</summary>
    public List<TestStepConfigurationInfo> Steps { get; init; } = new();
    
    /// <summary>所需仪器</summary>
    public List<string> RequiredInstruments { get; init; } = new();
    
    /// <summary>所需DUT</summary>
    public List<string> RequiredDUTs { get; init; } = new();
    
    /// <summary>预计执行时间</summary>
    public TimeSpan EstimatedDuration { get; init; }
    
    /// <summary>并行执行</summary>
    public bool AllowParallelExecution { get; init; } = false;
    
    /// <summary>失败时停止</summary>
    public bool StopOnFailure { get; init; } = true;
    
    /// <summary>标签</summary>
    public List<string> Tags { get; init; } = new();
    
    /// <summary>全局变量</summary>
    public Dictionary<string, object> GlobalVariables { get; init; } = new();
    
    /// <summary>扩展属性</summary>
    public Dictionary<string, object> Properties { get; init; } = new();
}

/// <summary>
/// 测试步骤配置信息
/// </summary>
public sealed record TestStepConfigurationInfo
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
    
    /// <summary>期望范围</summary>
    public ExpectedRangeInfo? ExpectedRange { get; init; }
    
    /// <summary>单位</summary>
    public string Unit { get; init; } = string.Empty;
    
    /// <summary>超时时间(毫秒)</summary>
    public int TimeoutMs { get; init; } = 30000;
    
    /// <summary>重试次数</summary>
    public int RetryCount { get; init; } = 0;
    
    /// <summary>延迟时间(毫秒)</summary>
    public int DelayAfterMs { get; init; } = 0;
    
    /// <summary>是否关键步骤</summary>
    public bool IsCritical { get; init; } = true;
    
    /// <summary>前置条件</summary>
    public List<string> Prerequisites { get; init; } = new();
    
    /// <summary>清理操作</summary>
    public List<string> CleanupActions { get; init; } = new();
    
    /// <summary>变量存储名称</summary>
    public string? StoreResultAs { get; init; }
    
    /// <summary>条件表达式</summary>
    public string? ConditionExpression { get; init; }
}

/// <summary>
/// 期望范围信息
/// </summary>
public sealed record ExpectedRangeInfo
{
    /// <summary>最小值</summary>
    public double Min { get; init; }
    
    /// <summary>最大值</summary>
    public double Max { get; init; }
    
    /// <summary>单位</summary>
    public string? Unit { get; init; }
}

/// <summary>
/// 系统配置信息
/// </summary>
public sealed record SystemConfigurationInfo
{
    /// <summary>系统名称</summary>
    public string SystemName { get; init; } = string.Empty;
    
    /// <summary>版本号</summary>
    public string Version { get; init; } = string.Empty;
    
    /// <summary>工作目录</summary>
    public string WorkingDirectory { get; init; } = string.Empty;
    
    /// <summary>数据目录</summary>
    public string DataDirectory { get; init; } = string.Empty;
    
    /// <summary>日志配置</summary>
    public LoggingConfigurationInfo Logging { get; init; } = new();
    
    /// <summary>数据库配置</summary>
    public DatabaseConfigurationInfo? Database { get; init; }
    
    /// <summary>最大并行任务数</summary>
    public int MaxParallelTasks { get; init; } = 4;
    
    /// <summary>任务超时时间(秒)</summary>
    public int TaskTimeoutSeconds { get; init; } = 3600;
    
    /// <summary>自动保存间隔(秒)</summary>
    public int AutoSaveIntervalSeconds { get; init; } = 300;
    
    /// <summary>语言设置</summary>
    public string Language { get; init; } = "zh-CN";
    
    /// <summary>时区设置</summary>
    public string TimeZone { get; init; } = string.Empty;
    
    /// <summary>安全配置</summary>
    public SecurityConfigurationInfo Security { get; init; } = new();
    
    /// <summary>多DUT测试配置</summary>
    public MultiDUTConfigurationInfo MultiDUT { get; init; } = new();
    
    /// <summary>扩展设置</summary>
    public Dictionary<string, object> ExtendedSettings { get; init; } = new();
}

/// <summary>
/// 多DUT测试配置信息
/// </summary>
public sealed record MultiDUTConfigurationInfo
{
    /// <summary>是否启用多DUT测试</summary>
    public bool EnableMultiDUTTesting { get; init; } = true;
    
    /// <summary>最大同时测试DUT数量</summary>
    public int MaxConcurrentDUTs { get; init; } = 10;
    
    /// <summary>DUT总数限制</summary>
    public int MaxTotalDUTs { get; init; } = 50;
    
    /// <summary>默认DUT测试超时时间(分钟)</summary>
    public int DefaultDUTTimeoutMinutes { get; init; } = 30;
    
    /// <summary>DUT故障隔离策略</summary>
    public string FailureIsolationStrategy { get; init; } = "Isolate"; // Isolate, Continue, Stop
    
    /// <summary>资源分配策略</summary>
    public string ResourceAllocationStrategy { get; init; } = "LoadBalanced"; // FIFO, LoadBalanced, Priority
    
    /// <summary>DUT调度间隔(毫秒)</summary>
    public int SchedulingIntervalMs { get; init; } = 1000;
    
    /// <summary>启用DUT负载均衡</summary>
    public bool EnableLoadBalancing { get; init; } = true;
    
    /// <summary>启用DUT优先级调度</summary>
    public bool EnablePriorityScheduling { get; init; } = true;
    
    /// <summary>启用动态资源分配</summary>
    public bool EnableDynamicResourceAllocation { get; init; } = true;
    
    /// <summary>DUT连接池大小</summary>
    public int ConnectionPoolSize { get; init; } = 20;
    
    /// <summary>连接超时时间(秒)</summary>
    public int ConnectionTimeoutSeconds { get; init; } = 30;
    
    /// <summary>连接重试次数</summary>
    public int ConnectionRetryCount { get; init; } = 3;
    
    /// <summary>启用DUT状态监控</summary>
    public bool EnableStatusMonitoring { get; init; } = true;
    
    /// <summary>状态监控间隔(秒)</summary>
    public int StatusMonitoringIntervalSeconds { get; init; } = 10;
    
    /// <summary>启用测试数据实时收集</summary>
    public bool EnableRealtimeDataCollection { get; init; } = true;
    
    /// <summary>数据收集间隔(毫秒)</summary>
    public int DataCollectionIntervalMs { get; init; } = 500;
    
    /// <summary>启用并发测试报告</summary>
    public bool EnableConcurrentReporting { get; init; } = true;
    
    /// <summary>测试会话保留时间(小时)</summary>
    public int SessionRetentionHours { get; init; } = 24;
    
    /// <summary>DUT分组策略</summary>
    public List<DUTGroupConfigurationInfo> DUTGroups { get; init; } = new();
    
    /// <summary>扩展配置</summary>
    public Dictionary<string, object> ExtendedConfiguration { get; init; } = new();
}

/// <summary>
/// DUT分组配置信息
/// </summary>
public sealed record DUTGroupConfigurationInfo
{
    /// <summary>分组ID</summary>
    public string GroupId { get; init; } = string.Empty;
    
    /// <summary>分组名称</summary>
    public string GroupName { get; init; } = string.Empty;
    
    /// <summary>分组描述</summary>
    public string Description { get; init; } = string.Empty;
    
    /// <summary>最大并发数</summary>
    public int MaxConcurrent { get; init; } = 5;
    
    /// <summary>优先级</summary>
    public int Priority { get; init; } = 0;
    
    /// <summary>DUT模式匹配</summary>
    public List<string> DUTPatterns { get; init; } = new();
    
    /// <summary>资源需求</summary>
    public Dictionary<string, int> ResourceRequirements { get; init; } = new();
    
    /// <summary>调度策略</summary>
    public string SchedulingStrategy { get; init; } = "FIFO";
    
    /// <summary>标签</summary>
    public List<string> Tags { get; init; } = new();
    
    /// <summary>约束条件</summary>
    public Dictionary<string, object> Constraints { get; init; } = new();
}

/// <summary>
/// 日志配置信息
/// </summary>
public sealed record LoggingConfigurationInfo
{
    /// <summary>日志级别</summary>
    public string LogLevel { get; init; } = "Info";
    
    /// <summary>日志文件路径</summary>
    public string LogFilePath { get; init; } = string.Empty;
    
    /// <summary>最大文件大小(MB)</summary>
    public int MaxFileSizeMB { get; init; } = 100;
    
    /// <summary>保留天数</summary>
    public int RetentionDays { get; init; } = 30;
    
    /// <summary>是否启用控制台输出</summary>
    public bool EnableConsole { get; init; } = true;
    
    /// <summary>是否启用文件输出</summary>
    public bool EnableFile { get; init; } = true;
    
    /// <summary>是否启用数据库输出</summary>
    public bool EnableDatabase { get; init; } = false;
    
    /// <summary>扩展配置</summary>
    public Dictionary<string, object> ExtendedConfiguration { get; init; } = new();
}

/// <summary>
/// 数据库配置信息
/// </summary>
public sealed record DatabaseConfigurationInfo
{
    /// <summary>数据库类型</summary>
    public string DatabaseType { get; init; } = string.Empty;
    
    /// <summary>连接字符串</summary>
    public string ConnectionString { get; init; } = string.Empty;
    
    /// <summary>命令超时(秒)</summary>
    public int CommandTimeoutSeconds { get; init; } = 30;
    
    /// <summary>连接池大小</summary>
    public int ConnectionPoolSize { get; init; } = 10;
    
    /// <summary>是否启用迁移</summary>
    public bool EnableMigrations { get; init; } = true;
    
    /// <summary>扩展配置</summary>
    public Dictionary<string, object> ExtendedConfiguration { get; init; } = new();
}

/// <summary>
/// 安全配置信息
/// </summary>
public sealed record SecurityConfigurationInfo
{
    /// <summary>是否启用认证</summary>
    public bool EnableAuthentication { get; init; } = false;
    
    /// <summary>是否启用授权</summary>
    public bool EnableAuthorization { get; init; } = false;
    
    /// <summary>会话超时时间(分钟)</summary>
    public int SessionTimeoutMinutes { get; init; } = 60;
    
    /// <summary>密码策略</summary>
    public PasswordPolicyInfo PasswordPolicy { get; init; } = new();
    
    /// <summary>扩展配置</summary>
    public Dictionary<string, object> ExtendedConfiguration { get; init; } = new();
}

/// <summary>
/// 密码策略信息
/// </summary>
public sealed record PasswordPolicyInfo
{
    /// <summary>最小长度</summary>
    public int MinLength { get; init; } = 8;
    
    /// <summary>需要大写字母</summary>
    public bool RequireUppercase { get; init; } = true;
    
    /// <summary>需要小写字母</summary>
    public bool RequireLowercase { get; init; } = true;
    
    /// <summary>需要数字</summary>
    public bool RequireDigits { get; init; } = true;
    
    /// <summary>需要特殊字符</summary>
    public bool RequireSpecialCharacters { get; init; } = false;
}

/// <summary>
/// 配置变更事件参数
/// </summary>
public sealed class ConfigurationChangedEventArgs : EventArgs
{
    public string ConfigurationPath { get; init; } = string.Empty;
    public string ChangeType { get; init; } = string.Empty;
    public object? OldValue { get; init; }
    public object? NewValue { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// 配置提供者接口
/// </summary>
public interface IConfigurationProvider
{
    /// <summary>支持的配置格式</summary>
    IReadOnlyList<ConfigurationFormat> SupportedFormats { get; }
    
    /// <summary>配置变更事件</summary>
    event EventHandler<ConfigurationChangedEventArgs>? ConfigurationChanged;
    
    /// <summary>初始化配置提供者</summary>
    Task<bool> InitializeAsync(string configurationDirectory, CancellationToken cancellationToken = default);
    
    /// <summary>加载系统配置</summary>
    Task<SystemConfigurationInfo> LoadSystemConfigurationAsync(string? filePath = null, CancellationToken cancellationToken = default);
    
    /// <summary>保存系统配置</summary>
    Task<bool> SaveSystemConfigurationAsync(SystemConfigurationInfo configuration, string? filePath = null, CancellationToken cancellationToken = default);
    
    /// <summary>加载设备配置</summary>
    Task<List<DeviceConfigurationInfo>> LoadDeviceConfigurationAsync(string? filePath = null, CancellationToken cancellationToken = default);
    
    /// <summary>保存设备配置</summary>
    Task<bool> SaveDeviceConfigurationAsync(List<DeviceConfigurationInfo> configurations, string? filePath = null, CancellationToken cancellationToken = default);
    
    /// <summary>加载测试序列配置</summary>
    Task<List<TestSequenceConfigurationInfo>> LoadTestSequenceConfigurationAsync(string? filePath = null, CancellationToken cancellationToken = default);
    
    /// <summary>保存测试序列配置</summary>
    Task<bool> SaveTestSequenceConfigurationAsync(List<TestSequenceConfigurationInfo> configurations, string? filePath = null, CancellationToken cancellationToken = default);
    
    /// <summary>验证配置</summary>
    Task<ConfigurationValidationResult> ValidateConfigurationAsync(string filePath, ConfigurationFormat format, CancellationToken cancellationToken = default);
    
    /// <summary>监控配置文件变更</summary>
    Task<bool> StartMonitoringAsync(string configurationPath, CancellationToken cancellationToken = default);
    
    /// <summary>停止监控配置文件变更</summary>
    Task<bool> StopMonitoringAsync(CancellationToken cancellationToken = default);
    
    /// <summary>获取配置文件路径</summary>
    string GetConfigurationFilePath(string configurationName, ConfigurationFormat format);
    
    /// <summary>备份配置</summary>
    Task<bool> BackupConfigurationAsync(string sourcePath, string backupPath, CancellationToken cancellationToken = default);
    
    /// <summary>恢复配置</summary>
    Task<bool> RestoreConfigurationAsync(string backupPath, string targetPath, CancellationToken cancellationToken = default);
    
    /// <summary>导出配置</summary>
    Task<bool> ExportConfigurationAsync(string sourcePath, string exportPath, ConfigurationFormat exportFormat, CancellationToken cancellationToken = default);
    
    /// <summary>导入配置</summary>
    Task<bool> ImportConfigurationAsync(string importPath, string targetPath, ConfigurationFormat importFormat, CancellationToken cancellationToken = default);
    
    /// <summary>合并配置</summary>
    Task<bool> MergeConfigurationAsync(string baseConfigPath, string overrideConfigPath, string outputPath, CancellationToken cancellationToken = default);
}
