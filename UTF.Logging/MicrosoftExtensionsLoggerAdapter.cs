using System;
using Microsoft.Extensions.Logging;

namespace UTF.Logging;

/// <summary>
/// <see cref="Microsoft.Extensions.Logging.ILoggerProvider"/> 适配器，将
/// <see cref="UTF.Logging.ILogger"/> 桥接为 <c>Microsoft.Extensions.Logging</c> 体系，
/// 便于在依赖注入场景下复用统一日志记录器。
/// </summary>
public sealed class UtfLoggerProvider : ILoggerProvider
{
    private readonly UTF.Logging.ILogger _utfLogger;
    private bool _disposed;

    /// <summary>
    /// 创建适配器实例。传入的 <paramref name="utfLogger"/> 生命周期由调用方管理，
    /// 本类型释放时不会释放该记录器。
    /// </summary>
    public UtfLoggerProvider(UTF.Logging.ILogger utfLogger)
    {
        _utfLogger = utfLogger ?? throw new ArgumentNullException(nameof(utfLogger));
    }

    /// <summary>
    /// 创建一个以 <paramref name="categoryName"/> 为源的桥接记录器。
    /// </summary>
    public Microsoft.Extensions.Logging.ILogger CreateLogger(string categoryName)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(UtfLoggerProvider));
        return new UtfLoggerBridge(_utfLogger, categoryName);
    }

    /// <summary>
    /// 释放适配器。不释放底层 <see cref="UTF.Logging.ILogger"/>——其生命周期由调用方负责。
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 桥接记录器：将 <c>Microsoft.Extensions.Logging</c> 的日志调用转发给
    /// <see cref="UTF.Logging.ILogger"/>，并按级别映射。
    /// </summary>
    private sealed class UtfLoggerBridge : Microsoft.Extensions.Logging.ILogger
    {
        private readonly UTF.Logging.ILogger _utfLogger;
        private readonly string _source;

        public UtfLoggerBridge(UTF.Logging.ILogger utfLogger, string source)
        {
            _utfLogger = utfLogger;
            _source = source;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            // 作用域由 UTF.Logging.ILogger 自身的 CreateScopedLogger 提供；
            // 此处返回一个空作用域以保持接口契约。如需真实作用域，可在此创建
            // 作用域记录器并返回。当前实现返回一个空操作 Disposable。
            return NullScope.Instance;
        }

        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel)
        {
            // LogLevel.None 永远不记录
            return logLevel != Microsoft.Extensions.Logging.LogLevel.None;
        }

        public void Log<TState>(
            Microsoft.Extensions.Logging.LogLevel logLevel,
            Microsoft.Extensions.Logging.EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel == Microsoft.Extensions.Logging.LogLevel.None) return;
            if (formatter is null) return;

            var message = formatter(state, exception);
            if (string.IsNullOrEmpty(message) && exception is null) return;

            // 在消息中包含 EventId（若有）以便追溯
            var fullMessage = eventId.Id != 0
                ? $"[{eventId.Id}] {message}"
                : message;

            _utfLogger.Log(MapLevel(logLevel), fullMessage, _source, exception);
        }

        private static UTF.Logging.LogLevel MapLevel(Microsoft.Extensions.Logging.LogLevel logLevel) => logLevel switch
        {
            Microsoft.Extensions.Logging.LogLevel.Trace => UTF.Logging.LogLevel.Debug,
            Microsoft.Extensions.Logging.LogLevel.Debug => UTF.Logging.LogLevel.Debug,
            Microsoft.Extensions.Logging.LogLevel.Information => UTF.Logging.LogLevel.Info,
            Microsoft.Extensions.Logging.LogLevel.Warning => UTF.Logging.LogLevel.Warning,
            Microsoft.Extensions.Logging.LogLevel.Error => UTF.Logging.LogLevel.Error,
            Microsoft.Extensions.Logging.LogLevel.Critical => UTF.Logging.LogLevel.Critical,
            _ => UTF.Logging.LogLevel.Info
        };
    }

    /// <summary>
    /// 空作用域 Disposable，避免作用域未实现时抛出。
    /// </summary>
    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();
        private NullScope() { }
        public void Dispose() { }
    }
}
