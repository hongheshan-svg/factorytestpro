using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace UTF.Logging
{
    /// <summary>
    /// 高性能日志记录器实现
    /// </summary>
    public sealed class AdvancedLogger : ILogger, IDisposable
    {
        private readonly string _source;
        private readonly LogConfiguration _config;
        private readonly ConcurrentDictionary<string, object> _contextProperties = new();
        private readonly ConcurrentQueue<LogEntry> _logQueue = new();
        private readonly List<ILogWriter> _writers = new();
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private readonly Task _backgroundTask;
        private readonly SemaphoreSlim _flushSemaphore = new(1, 1);
        private bool _disposed;

        /// <summary>
        /// 日志写入事件
        /// </summary>
        public event EventHandler<LogEventArgs>? LogWritten;

        public AdvancedLogger(string source = "UTF", LogConfiguration? config = null)
        {
            _source = source;
            _config = config ?? new LogConfiguration();
            
            // 初始化日志写入器
            InitializeWriters();
            
            // 启动后台日志处理任务
            _backgroundTask = Task.Run(ProcessLogQueueAsync);
        }

        private void InitializeWriters()
        {
            // 控制台写入器
            if (_config.EnableConsole)
            {
                _writers.Add(new ConsoleLogWriter(_config));
            }

            // 文件写入器
            if (_config.EnableFile)
            {
                var logFilePath = string.IsNullOrEmpty(_config.LogFilePath) 
                    ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs", $"utf-{DateTime.Now:yyyy-MM-dd}.log")
                    : _config.LogFilePath;
                _writers.Add(new FileLogWriter(logFilePath, _config));
            }
        }

        public void Debug(string message, string? source = null, Dictionary<string, object>? properties = null)
        {
            Log(LogLevel.Debug, LogCategory.System, message, null, source, properties);
        }

        public void Info(string message, string? source = null, Dictionary<string, object>? properties = null)
        {
            Log(LogLevel.Info, LogCategory.System, message, null, source, properties);
        }

        public void Warning(string message, string? source = null, Dictionary<string, object>? properties = null)
        {
            Log(LogLevel.Warning, LogCategory.System, message, null, source, properties);
        }

        public void Error(string message, Exception? exception = null, string? source = null, Dictionary<string, object>? properties = null)
        {
            Log(LogLevel.Error, LogCategory.System, message, exception, source, properties);
        }

        public void Critical(string message, Exception? exception = null, string? source = null, Dictionary<string, object>? properties = null)
        {
            Log(LogLevel.Critical, LogCategory.System, message, exception, source, properties);
        }

        public void Log(LogLevel level, LogCategory category, string message, Exception? exception = null, string? source = null, Dictionary<string, object>? properties = null)
        {
            if (level < _config.MinLevel || _disposed) return;

            try
            {
                var logEntry = CreateLogEntry(level, category, message, exception, source ?? _source, properties);
                _logQueue.Enqueue(logEntry);
                
                // 触发日志写入事件
                LogWritten?.Invoke(this, new LogEventArgs { LogEntry = logEntry });
            }
            catch (Exception ex)
            {
                // 日志记录失败时输出到控制台
                Console.WriteLine($"[LOGGER ERROR] Failed to log message: {ex.Message}");
            }
        }

        public async Task DebugAsync(string message, string? source = null, Dictionary<string, object>? properties = null, CancellationToken cancellationToken = default)
        {
            await LogAsync(LogLevel.Debug, LogCategory.System, message, null, source, properties, cancellationToken);
        }

        public async Task InfoAsync(string message, string? source = null, Dictionary<string, object>? properties = null, CancellationToken cancellationToken = default)
        {
            await LogAsync(LogLevel.Info, LogCategory.System, message, null, source, properties, cancellationToken);
        }

        public async Task WarningAsync(string message, string? source = null, Dictionary<string, object>? properties = null, CancellationToken cancellationToken = default)
        {
            await LogAsync(LogLevel.Warning, LogCategory.System, message, null, source, properties, cancellationToken);
        }

        public async Task ErrorAsync(string message, Exception? exception = null, string? source = null, Dictionary<string, object>? properties = null, CancellationToken cancellationToken = default)
        {
            await LogAsync(LogLevel.Error, LogCategory.System, message, exception, source, properties, cancellationToken);
        }

        public async Task CriticalAsync(string message, Exception? exception = null, string? source = null, Dictionary<string, object>? properties = null, CancellationToken cancellationToken = default)
        {
            await LogAsync(LogLevel.Critical, LogCategory.System, message, exception, source, properties, cancellationToken);
        }

        public async Task LogAsync(LogLevel level, LogCategory category, string message, Exception? exception = null, string? source = null, Dictionary<string, object>? properties = null, CancellationToken cancellationToken = default)
        {
            if (level < _config.MinLevel || _disposed) return;

            try
            {
                var logEntry = CreateLogEntry(level, category, message, exception, source ?? _source, properties);
                _logQueue.Enqueue(logEntry);
                
                // 触发日志写入事件
                LogWritten?.Invoke(this, new LogEventArgs { LogEntry = logEntry });
                
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LOGGER ERROR] Failed to log message async: {ex.Message}");
            }
        }

        public ILogger CreateScopedLogger(string scope, Dictionary<string, object>? scopeProperties = null)
        {
            var scopedSource = string.IsNullOrEmpty(_source) ? scope : $"{_source}.{scope}";
            var scopedLogger = new AdvancedLogger(scopedSource, _config);
            
            if (scopeProperties != null)
            {
                foreach (var property in scopeProperties)
                {
                    scopedLogger.SetContextProperty(property.Key, property.Value);
                }
            }
            
            return scopedLogger;
        }

        public void SetContextProperty(string key, object value)
        {
            _contextProperties[key] = value;
        }

        public void RemoveContextProperty(string key)
        {
            _contextProperties.TryRemove(key, out _);
        }

        public void ClearContextProperties()
        {
            _contextProperties.Clear();
        }

        private LogEntry CreateLogEntry(LogLevel level, LogCategory category, string message, Exception? exception, string source, Dictionary<string, object>? properties)
        {
            // 准备属性字典
            var entryProperties = new Dictionary<string, object>();
            
            // 添加上下文属性
            foreach (var contextProperty in _contextProperties)
            {
                entryProperties[contextProperty.Key] = contextProperty.Value;
            }

            // 添加传入的属性
            if (properties != null)
            {
                foreach (var property in properties)
                {
                    entryProperties[property.Key] = property.Value;
                }
            }

            // 创建日志条目（使用初始化器设置所有属性）
            var logEntry = new LogEntry
            {
                Level = level,
                Category = category,
                Message = message,
                Exception = exception,
                Source = source,
                Timestamp = DateTime.UtcNow,
                Properties = entryProperties,
                StackTrace = (_config.IncludeStackTrace && level >= LogLevel.Error) ? Environment.StackTrace : null
            };

            return logEntry;
        }

        private async Task ProcessLogQueueAsync()
        {
            var logBuffer = new List<LogEntry>(100);
            
            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    logBuffer.Clear();
                    
                    // 批量处理日志
                    var processedCount = 0;
                    while (_logQueue.TryDequeue(out var entry) && processedCount < _config.BufferSize)
                    {
                        logBuffer.Add(entry);
                        processedCount++;
                    }

                    if (logBuffer.Count > 0)
                    {
                        // 批量写入所有写入器
                        var tasks = _writers.Select(writer => writer.WriteBatchAsync(logBuffer, _cancellationTokenSource.Token));
                        await Task.WhenAll(tasks);
                    }
                    else
                    {
                        // 没有日志时等待
                        await Task.Delay(_config.FlushIntervalMs, _cancellationTokenSource.Token);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[LOGGER BACKGROUND ERROR] {ex.Message}");
                    await Task.Delay(1000, _cancellationTokenSource.Token);
                }
            }
        }

        /// <summary>
        /// 手动刷新日志缓冲区
        /// </summary>
        public async Task FlushAsync()
        {
            await _flushSemaphore.WaitAsync();
            try
            {
                var tasks = _writers.Select(writer => writer.FlushAsync());
                await Task.WhenAll(tasks);
            }
            finally
            {
                _flushSemaphore.Release();
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _cancellationTokenSource.Cancel();

            try
            {
                _backgroundTask.Wait(TimeSpan.FromSeconds(5));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LOGGER DISPOSE ERROR] {ex.Message}");
            }

            // 处理剩余的日志
            var remainingLogs = new List<LogEntry>();
            while (_logQueue.TryDequeue(out var entry))
            {
                remainingLogs.Add(entry);
            }

            if (remainingLogs.Count > 0)
            {
                foreach (var writer in _writers)
                {
                    try
                    {
                        writer.WriteBatchAsync(remainingLogs).Wait(5000);
                        writer.FlushAsync().Wait(1000);
                    }
                    catch { }
                }
            }

            // 释放写入器
            foreach (var writer in _writers)
            {
                try
                {
                    writer.CloseAsync().Wait(1000);
                    if (writer is IDisposable disposable)
                        disposable.Dispose();
                }
                catch { }
            }

            _cancellationTokenSource.Dispose();
            _flushSemaphore.Dispose();
        }
    }

    /// <summary>
    /// 控制台日志写入器
    /// </summary>
    public sealed class ConsoleLogWriter : ILogWriter, IDisposable
    {
        private readonly LogConfiguration _config;
        private readonly object _lock = new();

        public ConsoleLogWriter(LogConfiguration config)
        {
            _config = config;
        }

        public Task WriteAsync(LogEntry logEntry, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                WriteToConsole(logEntry);
            }
            return Task.CompletedTask;
        }

        public Task WriteBatchAsync(IEnumerable<LogEntry> logEntries, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                foreach (var entry in logEntries)
                {
                    WriteToConsole(entry);
                }
            }
            return Task.CompletedTask;
        }

        private void WriteToConsole(LogEntry entry)
        {
            var originalColor = Console.ForegroundColor;
            try
            {
                Console.ForegroundColor = GetLogLevelColor(entry.Level);
                
                var message = FormatLogEntry(entry);
                Console.WriteLine(message);
                
                if (entry.Exception != null)
                {
                    Console.WriteLine($"Exception: {entry.Exception}");
                }
            }
            finally
            {
                Console.ForegroundColor = originalColor;
            }
        }

        private string FormatLogEntry(LogEntry entry)
        {
            return _config.FormatTemplate
                .Replace("{Timestamp:yyyy-MM-dd HH:mm:ss.fff}", entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff"))
                .Replace("{Level}", entry.Level.ToString())
                .Replace("{Category}", entry.Category.ToString())
                .Replace("{Source}", entry.Source)
                .Replace("{Message}", entry.Message);
        }

        private static ConsoleColor GetLogLevelColor(LogLevel level) => level switch
        {
            LogLevel.Debug => ConsoleColor.Gray,
            LogLevel.Info => ConsoleColor.Green,
            LogLevel.Warning => ConsoleColor.Yellow,
            LogLevel.Error => ConsoleColor.Red,
            LogLevel.Critical => ConsoleColor.Magenta,
            _ => ConsoleColor.White
        };

        public Task FlushAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task CloseAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public void Dispose() { }
    }

    /// <summary>
    /// 文件日志写入器
    /// </summary>
    public sealed class FileLogWriter : ILogWriter, IDisposable
    {
        private readonly string _filePath;
        private readonly LogConfiguration _config;
        private readonly SemaphoreSlim _semaphore = new(1, 1);
        private bool _disposed;

        public FileLogWriter(string filePath, LogConfiguration config)
        {
            _filePath = filePath;
            _config = config;
            
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        public async Task WriteAsync(LogEntry logEntry, CancellationToken cancellationToken = default)
        {
            if (_disposed) return;

            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                var logLine = FormatLogEntry(logEntry);
                await File.AppendAllTextAsync(_filePath, logLine + Environment.NewLine, cancellationToken);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task WriteBatchAsync(IEnumerable<LogEntry> logEntries, CancellationToken cancellationToken = default)
        {
            if (_disposed) return;

            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                var lines = logEntries.Select(FormatLogEntry).ToList();
                var content = string.Join(Environment.NewLine, lines) + Environment.NewLine;
                await File.AppendAllTextAsync(_filePath, content, cancellationToken);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private string FormatLogEntry(LogEntry entry)
        {
            var formattedMessage = _config.FormatTemplate
                .Replace("{Timestamp:yyyy-MM-dd HH:mm:ss.fff}", entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff"))
                .Replace("{Level}", entry.Level.ToString())
                .Replace("{Category}", entry.Category.ToString())
                .Replace("{Source}", entry.Source)
                .Replace("{Message}", entry.Message);

            if (entry.Exception != null)
            {
                formattedMessage += $" | Exception: {entry.Exception.Message}";
                if (!string.IsNullOrEmpty(entry.Exception.StackTrace))
                {
                    formattedMessage += $" | StackTrace: {entry.Exception.StackTrace}";
                }
            }

            return formattedMessage;
        }

        public Task FlushAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task CloseAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _semaphore.Dispose();
            }
        }
    }

    /// <summary>
    /// 日志工厂
    /// </summary>
    public static class LoggerFactory
    {
        private static readonly ConcurrentDictionary<string, ILogger> _loggers = new();
        private static LogConfiguration _globalConfig = new();

        /// <summary>
        /// 设置全局日志配置
        /// </summary>
        public static void SetGlobalConfiguration(LogConfiguration config)
        {
            _globalConfig = config;
        }

        /// <summary>
        /// 创建日志记录器
        /// </summary>
        public static ILogger CreateLogger(string source = "UTF")
        {
            return _loggers.GetOrAdd(source, s => new AdvancedLogger(s, _globalConfig));
        }

        /// <summary>
        /// 创建类型化日志记录器
        /// </summary>
        public static ILogger CreateLogger<T>()
        {
            return CreateLogger(typeof(T).Name);
        }

        /// <summary>
        /// 创建日志记录器（带配置）
        /// </summary>
        public static ILogger CreateLogger(string source, LogConfiguration config)
        {
            return new AdvancedLogger(source, config);
        }

        /// <summary>
        /// 释放所有日志记录器
        /// </summary>
        public static void DisposeAll()
        {
            foreach (var logger in _loggers.Values)
            {
                if (logger is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            _loggers.Clear();
        }
    }
}
