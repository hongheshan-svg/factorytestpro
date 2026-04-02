using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace UTF.Logging;

/// <summary>
/// 日志级别枚举
/// </summary>
public enum LogLevel
{
    /// <summary>调试</summary>
    Debug = 0,
    /// <summary>信息</summary>
    Info = 1,
    /// <summary>警告</summary>
    Warning = 2,
    /// <summary>错误</summary>
    Error = 3,
    /// <summary>严重错误</summary>
    Critical = 4
}

/// <summary>
/// 日志类别枚举
/// </summary>
public enum LogCategory
{
    /// <summary>系统</summary>
    System,
    /// <summary>测试</summary>
    Test,
    /// <summary>设备</summary>
    Device,
    /// <summary>通信</summary>
    Communication,
    /// <summary>用户操作</summary>
    UserAction,
    /// <summary>性能</summary>
    Performance,
    /// <summary>安全</summary>
    Security,
    /// <summary>审计</summary>
    Audit,
    /// <summary>自定义</summary>
    Custom
}

/// <summary>
/// 日志条目
/// </summary>
public sealed record LogEntry
{
    /// <summary>日志ID</summary>
    public string LogId { get; init; } = Guid.NewGuid().ToString();
    
    /// <summary>时间戳</summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    
    /// <summary>日志级别</summary>
    public LogLevel Level { get; init; }
    
    /// <summary>日志类别</summary>
    public LogCategory Category { get; init; }
    
    /// <summary>消息</summary>
    public string Message { get; init; } = string.Empty;
    
    /// <summary>详细信息</summary>
    public string? Details { get; init; }
    
    /// <summary>异常信息</summary>
    public Exception? Exception { get; init; }
    
    /// <summary>源组件</summary>
    public string Source { get; init; } = string.Empty;
    
    /// <summary>线程ID</summary>
    public int ThreadId { get; init; } = Thread.CurrentThread.ManagedThreadId;
    
    /// <summary>用户ID</summary>
    public string? UserId { get; init; }
    
    /// <summary>会话ID</summary>
    public string? SessionId { get; init; }
    
    /// <summary>任务ID</summary>
    public string? TaskId { get; init; }
    
    /// <summary>设备ID</summary>
    public string? DeviceId { get; init; }
    
    /// <summary>扩展属性</summary>
    public Dictionary<string, object> Properties { get; init; } = new();
    
    /// <summary>调用堆栈</summary>
    public string? StackTrace { get; init; }
}

/// <summary>
/// 日志过滤器
/// </summary>
public sealed record LogFilter
{
    /// <summary>最小日志级别</summary>
    public LogLevel? MinLevel { get; init; }
    
    /// <summary>最大日志级别</summary>
    public LogLevel? MaxLevel { get; init; }
    
    /// <summary>日志类别</summary>
    public List<LogCategory>? Categories { get; init; }
    
    /// <summary>源组件</summary>
    public List<string>? Sources { get; init; }
    
    /// <summary>时间范围开始</summary>
    public DateTime? StartTime { get; init; }
    
    /// <summary>时间范围结束</summary>
    public DateTime? EndTime { get; init; }
    
    /// <summary>关键字</summary>
    public string? Keywords { get; init; }
    
    /// <summary>用户ID</summary>
    public string? UserId { get; init; }
    
    /// <summary>任务ID</summary>
    public string? TaskId { get; init; }
    
    /// <summary>设备ID</summary>
    public string? DeviceId { get; init; }
}

/// <summary>
/// 日志配置
/// </summary>
public sealed record LogConfiguration
{
    /// <summary>最小日志级别</summary>
    public LogLevel MinLevel { get; init; } = LogLevel.Info;
    
    /// <summary>是否启用控制台输出</summary>
    public bool EnableConsole { get; init; } = true;
    
    /// <summary>是否启用文件输出</summary>
    public bool EnableFile { get; init; } = true;
    
    /// <summary>是否启用数据库输出</summary>
    public bool EnableDatabase { get; init; } = false;
    
    /// <summary>日志文件路径</summary>
    public string LogFilePath { get; init; } = string.Empty;
    
    /// <summary>日志文件最大大小(MB)</summary>
    public int MaxFileSizeMB { get; init; } = 100;
    
    /// <summary>日志文件保留天数</summary>
    public int RetentionDays { get; init; } = 30;
    
    /// <summary>是否自动滚动文件</summary>
    public bool AutoRollFiles { get; init; } = true;
    
    /// <summary>日志格式模板</summary>
    public string FormatTemplate { get; init; } = "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{Level}] [{Category}] [{Source}] {Message}";
    
    /// <summary>是否异步写入</summary>
    public bool AsyncWrite { get; init; } = true;
    
    /// <summary>缓冲区大小</summary>
    public int BufferSize { get; init; } = 1000;
    
    /// <summary>刷新间隔(毫秒)</summary>
    public int FlushIntervalMs { get; init; } = 1000;
    
    /// <summary>是否包含堆栈跟踪</summary>
    public bool IncludeStackTrace { get; init; } = false;
    
    /// <summary>性能计数器采样间隔(毫秒)</summary>
    public int PerformanceCounterIntervalMs { get; init; } = 5000;
    
    /// <summary>扩展配置</summary>
    public Dictionary<string, object> ExtendedConfiguration { get; init; } = new();
}

/// <summary>
/// 日志事件参数
/// </summary>
public sealed class LogEventArgs : EventArgs
{
    public LogEntry LogEntry { get; init; } = new();
}

/// <summary>
/// 日志写入器接口
/// </summary>
public interface ILogWriter
{
    /// <summary>写入日志</summary>
    Task WriteAsync(LogEntry logEntry, CancellationToken cancellationToken = default);
    
    /// <summary>批量写入日志</summary>
    Task WriteBatchAsync(IEnumerable<LogEntry> logEntries, CancellationToken cancellationToken = default);
    
    /// <summary>刷新缓冲区</summary>
    Task FlushAsync(CancellationToken cancellationToken = default);
    
    /// <summary>关闭写入器</summary>
    Task CloseAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// 日志记录器接口
/// </summary>
public interface ILogger
{
    /// <summary>记录调试信息</summary>
    void Debug(string message, string? source = null, Dictionary<string, object>? properties = null);
    
    /// <summary>记录信息</summary>
    void Info(string message, string? source = null, Dictionary<string, object>? properties = null);
    
    /// <summary>记录警告</summary>
    void Warning(string message, string? source = null, Dictionary<string, object>? properties = null);
    
    /// <summary>记录错误</summary>
    void Error(string message, Exception? exception = null, string? source = null, Dictionary<string, object>? properties = null);
    
    /// <summary>记录严重错误</summary>
    void Critical(string message, Exception? exception = null, string? source = null, Dictionary<string, object>? properties = null);
    
    /// <summary>记录自定义日志</summary>
    void Log(LogLevel level, LogCategory category, string message, Exception? exception = null, string? source = null, Dictionary<string, object>? properties = null);
    
    /// <summary>异步记录调试信息</summary>
    Task DebugAsync(string message, string? source = null, Dictionary<string, object>? properties = null, CancellationToken cancellationToken = default);
    
    /// <summary>异步记录信息</summary>
    Task InfoAsync(string message, string? source = null, Dictionary<string, object>? properties = null, CancellationToken cancellationToken = default);
    
    /// <summary>异步记录警告</summary>
    Task WarningAsync(string message, string? source = null, Dictionary<string, object>? properties = null, CancellationToken cancellationToken = default);
    
    /// <summary>异步记录错误</summary>
    Task ErrorAsync(string message, Exception? exception = null, string? source = null, Dictionary<string, object>? properties = null, CancellationToken cancellationToken = default);
    
    /// <summary>异步记录严重错误</summary>
    Task CriticalAsync(string message, Exception? exception = null, string? source = null, Dictionary<string, object>? properties = null, CancellationToken cancellationToken = default);
    
    /// <summary>异步记录自定义日志</summary>
    Task LogAsync(LogLevel level, LogCategory category, string message, Exception? exception = null, string? source = null, Dictionary<string, object>? properties = null, CancellationToken cancellationToken = default);
    
    /// <summary>创建作用域日志记录器</summary>
    ILogger CreateScopedLogger(string scope, Dictionary<string, object>? scopeProperties = null);
    
    /// <summary>设置上下文属性</summary>
    void SetContextProperty(string key, object value);
    
    /// <summary>移除上下文属性</summary>
    void RemoveContextProperty(string key);
    
    /// <summary>清空上下文属性</summary>
    void ClearContextProperties();
}
