using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace UTF.Logging
{
    /// <summary>
    /// 简单的日志记录器实现
    /// </summary>
    public class SimpleLogger : ILogger
    {
        public void LogDebug(string message) => Console.WriteLine($"[DEBUG] {DateTime.Now:HH:mm:ss} {message}");
        public void LogInfo(string message) => Console.WriteLine($"[INFO] {DateTime.Now:HH:mm:ss} {message}");
        public void LogWarning(string message) => Console.WriteLine($"[WARN] {DateTime.Now:HH:mm:ss} {message}");
        public void LogError(string message) => Console.WriteLine($"[ERROR] {DateTime.Now:HH:mm:ss} {message}");
        public void LogCritical(string message) => Console.WriteLine($"[CRITICAL] {DateTime.Now:HH:mm:ss} {message}");

        public void Debug(string message, string? category = null, Dictionary<string, object>? properties = null) => LogDebug(message);
        public void Info(string message, string? category = null, Dictionary<string, object>? properties = null) => LogInfo(message);
        public void Warning(string message, string? category = null, Dictionary<string, object>? properties = null) => LogWarning(message);
        public void Error(string message, Exception? exception = null, string? category = null, Dictionary<string, object>? properties = null) => LogError($"{message} {exception?.Message}");
        public void Critical(string message, Exception? exception = null, string? category = null, Dictionary<string, object>? properties = null) => LogCritical($"{message} {exception?.Message}");

        public void Log(LogLevel level, LogCategory category, string message, Exception? exception = null, string? categoryName = null, Dictionary<string, object>? properties = null)
        {
            switch (level)
            {
                case LogLevel.Debug: LogDebug(message); break;
                case LogLevel.Info: LogInfo(message); break;
                case LogLevel.Warning: LogWarning(message); break;
                case LogLevel.Error: LogError(message); break;
                case LogLevel.Critical: LogCritical(message); break;
            }
        }

        public Task DebugAsync(string message, string? category = null, Dictionary<string, object>? properties = null, CancellationToken cancellationToken = default)
        {
            LogDebug(message);
            return Task.CompletedTask;
        }

        public Task InfoAsync(string message, string? category = null, Dictionary<string, object>? properties = null, CancellationToken cancellationToken = default)
        {
            LogInfo(message);
            return Task.CompletedTask;
        }

        public Task WarningAsync(string message, string? category = null, Dictionary<string, object>? properties = null, CancellationToken cancellationToken = default)
        {
            LogWarning(message);
            return Task.CompletedTask;
        }

        public Task ErrorAsync(string message, Exception? exception = null, string? category = null, Dictionary<string, object>? properties = null, CancellationToken cancellationToken = default)
        {
            LogError($"{message} {exception?.Message}");
            return Task.CompletedTask;
        }

        public Task CriticalAsync(string message, Exception? exception = null, string? category = null, Dictionary<string, object>? properties = null, CancellationToken cancellationToken = default)
        {
            LogCritical($"{message} {exception?.Message}");
            return Task.CompletedTask;
        }

        public Task LogAsync(LogLevel level, LogCategory category, string message, Exception? exception = null, string? categoryName = null, Dictionary<string, object>? properties = null, CancellationToken cancellationToken = default)
        {
            Log(level, category, message, exception, categoryName, properties);
            return Task.CompletedTask;
        }

    public Task FlushAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public ILogger CreateScopedLogger(string scope, Dictionary<string, object>? scopeProperties = null)
    {
        return new SimpleLogger(); // 简单实现，返回新的实例
    }

    public void SetContextProperty(string key, object value)
    {
        // 简单实现，暂时不做处理
    }

    public void RemoveContextProperty(string key)
    {
        // 简单实现，暂时不做处理
    }

    public void ClearContextProperties()
    {
        // 简单实现，暂时不做处理
    }

    public void Dispose() { }
}
}
