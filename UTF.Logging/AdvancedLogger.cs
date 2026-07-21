using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace UTF.Logging
{
    /// <summary>
    /// 高性能日志记录器实现
    /// </summary>
    public sealed class AdvancedLogger : ILogger, IDisposable, IAsyncDisposable
    {
        private readonly string _source;
        private readonly LogConfiguration _config;
        private readonly ConcurrentDictionary<string, object> _contextProperties = new();
        private readonly ConcurrentQueue<LogEntry> _logQueue = new();
        private readonly int _maxQueuedEntries;
        private long _queuedEntries;
        private long _droppedEntries;
        private readonly List<ILogWriter> _writers = new();
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private readonly Task _backgroundTask;
        private readonly SemaphoreSlim _flushSemaphore = new(1, 1);
        private bool _disposed;

        /// <summary>
        /// 日志写入事件
        /// </summary>
        public event EventHandler<LogEventArgs>? LogWritten;

        /// <summary>
        /// 触发 <see cref="LogWritten"/> 事件。逐个调用订阅者委托，
        /// 任一订阅者抛出异常不会影响其他订阅者或日志主流程，
        /// 异常将被记录到控制台。
        /// </summary>
        private void RaiseLogWritten(LogEntry logEntry)
        {
            var handlers = LogWritten;
            if (handlers is null) return;

            var args = new LogEventArgs { LogEntry = logEntry };
            foreach (var handler in handlers.GetInvocationList().Cast<EventHandler<LogEventArgs>>())
            {
                try
                {
                    handler(this, args);
                }
                catch (Exception ex)
                {
                    // 单个订阅者异常不应中断其他订阅者或日志写入
                    Console.WriteLine($"[LOGGER EVENT SUBSCRIBER ERROR] {ex.Message}");
                }
            }
        }

        public AdvancedLogger(string source = "UTF", LogConfiguration? config = null)
        {
            _source = source;
            _config = config ?? new LogConfiguration();
            _maxQueuedEntries = Math.Max(1000, _config.BufferSize * 20);
            
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
                if (!TryEnqueue(logEntry)) return;

                // 触发日志写入事件（逐个调用，单个订阅者异常不影响其他订阅者与日志主流程）
                RaiseLogWritten(logEntry);
            }
            catch (Exception ex)
            {
                // 日志记录失败时输出到控制台
                Console.WriteLine($"[LOGGER ERROR] Failed to log message: {ex.Message}");
            }
        }

        /// <summary>
        /// 简化日志重载：仅按级别与消息入队，可选源与异常。默认类别 <see cref="LogCategory.System"/>。
        /// </summary>
        public void Log(LogLevel level, string message, string? source = null, Exception? exception = null)
            => Log(level, LogCategory.System, message, exception, source, null);

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

        /// <summary>
        /// 异步记录日志。该方法为"提交后即忘"（fire-and-forget）：
        /// 仅将条目入队并触发事件后立即返回已完成任务，
        /// 实际写入由后台 <see cref="ProcessLogQueueAsync"/> 任务完成。
        /// </summary>
        public Task LogAsync(LogLevel level, LogCategory category, string message, Exception? exception = null, string? source = null, Dictionary<string, object>? properties = null, CancellationToken cancellationToken = default)
        {
            if (level < _config.MinLevel || _disposed) return Task.CompletedTask;

            try
            {
                var logEntry = CreateLogEntry(level, category, message, exception, source ?? _source, properties);
                if (!TryEnqueue(logEntry)) return Task.CompletedTask;

                // 触发日志写入事件（逐个调用，单个订阅者异常不影响其他订阅者与日志主流程）
                RaiseLogWritten(logEntry);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LOGGER ERROR] Failed to log message async: {ex.Message}");
            }

            // 入队后立即完成；实际写入在后台任务中进行
            return Task.CompletedTask;
        }

        /// <summary>
        /// 创建作用域日志记录器。
        /// 注意：返回的包装器与父记录器共享后台队列与写入器——不会启动新的后台任务，
        /// 仅在源名称与上下文属性上做"作用域"区分。包装器被释放时仅取消订阅事件，
        /// 不会停止父记录器的后台队列。
        /// </summary>
        public ILogger CreateScopedLogger(string scope, Dictionary<string, object>? scopeProperties = null)
        {
            var scopedSource = string.IsNullOrEmpty(_source) ? scope : $"{_source}.{scope}";
            return new ScopedLogger(this, scopedSource, scopeProperties);
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

        /// <summary>
        /// 供作用域包装器使用：以指定源与属性构建条目并入队到父记录器的共享队列。
        /// 不创建新的后台任务，复用父记录器的写入器。
        /// </summary>
        internal void EnqueueScoped(
            LogLevel level, LogCategory category, string message,
            Exception? exception, string source, Dictionary<string, object>? properties)
        {
            if (level < _config.MinLevel || _disposed) return;

            try
            {
                var logEntry = CreateLogEntry(level, category, message, exception, source, properties);
                if (!TryEnqueue(logEntry)) return;
                RaiseLogWritten(logEntry);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LOGGER ERROR] Failed to log scoped message: {ex.Message}");
            }
        }

        /// <summary>
        /// 供作用域包装器使用：返回父记录器当前是否已释放。
        /// </summary>
        internal bool IsDisposed => _disposed;

        private bool TryEnqueue(LogEntry entry)
        {
            var count = Interlocked.Increment(ref _queuedEntries);
            if (count <= _maxQueuedEntries)
            {
                _logQueue.Enqueue(entry);
                return true;
            }

            Interlocked.Decrement(ref _queuedEntries);
            if (entry.Level >= LogLevel.Error && _logQueue.TryDequeue(out _))
            {
                Interlocked.Decrement(ref _queuedEntries);
                Interlocked.Increment(ref _queuedEntries);
                _logQueue.Enqueue(entry);
                Interlocked.Increment(ref _droppedEntries);
                return true;
            }

            var dropped = Interlocked.Increment(ref _droppedEntries);
            if (dropped == 1 || dropped % 1000 == 0)
            {
                Console.WriteLine($"[LOGGER BACKPRESSURE] Dropped {dropped} queued log entries.");
            }

            return false;
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

                    // 先把队列一次性抽干（最多到缓冲区上限），避免突发写入被 FlushInterval 拖慢
                    var processedCount = 0;
                    while (processedCount < _config.BufferSize && _logQueue.TryDequeue(out var entry))
                    {
                        Interlocked.Decrement(ref _queuedEntries);
                        logBuffer.Add(entry);
                        processedCount++;
                    }

                    if (logBuffer.Count > 0)
                    {
                        // 批量写入所有写入器
                        var tasks = _writers.Select(writer => writer.WriteBatchAsync(logBuffer, _cancellationTokenSource.Token));
                        await Task.WhenAll(tasks).ConfigureAwait(false);

                        // 写完一批后立即继续抽干，若仍有积压则不再等待 Delay，
                        // 直到队列空才进入等待分支——避免一次突发需要数十秒才能排空
                        continue;
                    }

                    // 队列已空：等待下一个刷新周期或被取消
                    await Task.Delay(_config.FlushIntervalMs, _cancellationTokenSource.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[LOGGER BACKGROUND ERROR] {ex.Message}");
                    await Task.Delay(1000, _cancellationTokenSource.Token).ConfigureAwait(false);
                }
            }

            // 取消后做最后一次抽干，尽量不丢日志
            try
            {
                logBuffer.Clear();
                while (_logQueue.TryDequeue(out var entry))
                {
                    Interlocked.Decrement(ref _queuedEntries);
                    logBuffer.Add(entry);
                }

                if (logBuffer.Count > 0 && _writers.Count > 0)
                {
                    var tasks = _writers.Select(writer => writer.WriteBatchAsync(logBuffer, CancellationToken.None));
                    await Task.WhenAll(tasks).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LOGGER DRAIN ERROR] {ex.Message}");
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

            // 同步释放：取消后台任务后限时等待，并同步刷盘/关闭写入器。
            // 这里保留 .Wait(N) 是有意的同步释放路径（Dispose 契约要求同步完成），
            // 真正的异步释放请走 DisposeAsync 路径以避免 sync-over-async 死锁。
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
                Interlocked.Decrement(ref _queuedEntries);
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

            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 异步释放日志记录器。取消后台任务并 await 其抽干过程，
        /// 随后异步 flush 并关闭所有写入器，避免 sync-over-async 死锁。
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            _disposed = true;

            _cancellationTokenSource.Cancel();

            try
            {
                await _backgroundTask.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LOGGER DISPOSEASYNC ERROR] {ex.Message}");
            }

            // 处理剩余的日志
            var remainingLogs = new List<LogEntry>();
            while (_logQueue.TryDequeue(out var entry))
            {
                Interlocked.Decrement(ref _queuedEntries);
                remainingLogs.Add(entry);
            }

            if (remainingLogs.Count > 0)
            {
                foreach (var writer in _writers)
                {
                    try
                    {
                        await writer.WriteBatchAsync(remainingLogs).ConfigureAwait(false);
                        await writer.FlushAsync().ConfigureAwait(false);
                    }
                    catch { }
                }
            }

            // 释放写入器
            foreach (var writer in _writers)
            {
                try
                {
                    await writer.CloseAsync().ConfigureAwait(false);
                    if (writer is IDisposable disposable)
                        disposable.Dispose();
                }
                catch { }
            }

            _cancellationTokenSource.Dispose();
            _flushSemaphore.Dispose();

            GC.SuppressFinalize(this);
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
            return LogTemplateFormatter.ApplyTimestampTemplate(_config, entry.Timestamp)
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
    /// 文件日志写入器。使用一个长生命周期的 <see cref="FileStream"/>（以
    /// <see cref="FileShare.ReadWrite"/> 打开，便于外部进程并发读取）与一个
    /// <see cref="StreamWriter"/>（<see cref="StreamWriter.AutoFlush"/>=false）。
    /// 在 <see cref="WriteBatchAsync"/> 之外不主动刷盘；刷盘发生在
    /// <see cref="FlushAsync"/> 与 <see cref="Dispose"/>。当写入将超过
    /// <see cref="LogConfiguration.MaxFileSizeMB"/> 时滚动到新文件
    /// （<c>logfile.1.log</c>、<c>logfile.2.log</c>...），最多保留
    /// <see cref="LogConfiguration.AutoRollFiles"/> 路径族内的滚动文件——
    /// 为简单起见，本实现按序号递增滚动并在数量超出上限时删除最旧的滚动文件。
    /// </summary>
    public sealed class FileLogWriter : ILogWriter, IDisposable
    {
        private readonly string _filePath;
        private readonly LogConfiguration _config;
        private readonly SemaphoreSlim _semaphore = new(1, 1);
        private readonly long _maxFileSizeBytes;
        private FileStream? _fileStream;
        private StreamWriter? _streamWriter;
        private bool _disposed;

        public FileLogWriter(string filePath, LogConfiguration config)
        {
            _filePath = filePath;
            _config = config;
            _maxFileSizeBytes = Math.Max(1, config.MaxFileSizeMB) * 1024L * 1024L;

            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            OpenStream(append: true);
        }

        /// <summary>
        /// 以 <see cref="FileShare.ReadWrite"/> 打开文件流并包装一个
        /// <see cref="StreamWriter"/>（<see cref="StreamWriter.AutoFlush"/>=false）。
        /// </summary>
        private void OpenStream(bool append)
        {
            _streamWriter?.Dispose();
            _fileStream?.Dispose();

            var mode = append ? FileMode.Append : FileMode.Create;
            _fileStream = new FileStream(
                _filePath,
                mode,
                FileAccess.Write,
                FileShare.ReadWrite,
                bufferSize: 4096,
                options: FileOptions.None);
            _streamWriter = new StreamWriter(_fileStream)
            {
                AutoFlush = false,
            };
        }

        public async Task WriteAsync(LogEntry logEntry, CancellationToken cancellationToken = default)
        {
            if (_disposed) return;

            await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var logLine = FormatLogEntry(logEntry) + Environment.NewLine;
                await WriteInternalAsync(logLine, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task WriteBatchAsync(IEnumerable<LogEntry> logEntries, CancellationToken cancellationToken = default)
        {
            if (_disposed) return;

            await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                foreach (var entry in logEntries)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var line = FormatLogEntry(entry) + Environment.NewLine;
                    await WriteInternalAsync(line, cancellationToken).ConfigureAwait(false);
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// 将一行内容写入当前流；若超出最大文件大小则滚动到新文件后再写。
        /// </summary>
        private async Task WriteInternalAsync(string line, CancellationToken cancellationToken)
        {
            // 写入前检查文件大小（按 UTF-8 字节数估算）
            var lineByteCount = System.Text.Encoding.UTF8.GetByteCount(line);
            var currentLength = _fileStream?.Length ?? 0;

            if (currentLength + lineByteCount > _maxFileSizeBytes)
            {
                // 先把当前缓冲刷到磁盘，再滚动
                await FlushCoreAsync(cancellationToken).ConfigureAwait(false);
                RollToNextFile();
            }

            if (_streamWriter is null) return;
            await _streamWriter.WriteAsync(line.AsMemory(), cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// 滚动到下一个日志文件。基于 <see cref="AutoRollFiles"/> 上限删除最旧的滚动文件。
        /// </summary>
        private void RollToNextFile()
        {
            // 计算已存在的滚动文件序号
            var existing = GetExistingRollFiles().OrderByDescending(f => f.Sequence).ToList();

            // 删除超出上限的旧滚动文件
            var maxFiles = _config.AutoRollFiles ? Math.Max(1, 5) : 1;
            while (existing.Count >= maxFiles)
            {
                var oldest = existing[existing.Count - 1];
                existing.RemoveAt(existing.Count - 1);
                try { File.Delete(oldest.Path); }
                catch (Exception ex)
                {
                    Console.WriteLine($"[LOGGER ROLL DELETE ERROR] {ex.Message}");
                }
            }

            var nextSeq = existing.Count > 0 ? existing[0].Sequence + 1 : 1;
            var rolledPath = BuildRolledFilePath(nextSeq);
            var moved = false;

            _streamWriter?.Dispose();
            _streamWriter = null;
            _fileStream?.Dispose();
            _fileStream = null;

            try
            {
                // 将当前主文件重命名为滚动文件（若存在）
                if (File.Exists(_filePath))
                {
                    File.Move(_filePath, rolledPath, overwrite: true);
                    moved = true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LOGGER ROLL MOVE ERROR] {ex.Message}");
            }

            // If rolling fails, append to the existing file instead of truncating it.
            OpenStream(append: !moved);
        }

        private record RollFile(string Path, int Sequence);

        private List<RollFile> GetExistingRollFiles()
        {
            var result = new List<RollFile>();
            var dir = Path.GetDirectoryName(_filePath);
            var name = Path.GetFileNameWithoutExtension(_filePath);
            var ext = Path.GetExtension(_filePath);

            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return result;

            // 滚动文件命名：name.1.ext、name.2.ext ...
            var prefix = name + ".";
            foreach (var file in Directory.EnumerateFiles(dir, $"{name}.*{ext}"))
            {
                var fileName = Path.GetFileNameWithoutExtension(file);
                var idx = fileName.LastIndexOf('.');
                if (idx < 0 || idx >= fileName.Length - 1) continue;
                var seqPart = fileName[(idx + 1)..];
                if (int.TryParse(seqPart, out var seq) && seq > 0)
                {
                    // 确认前缀匹配，避免误删 name.othername.ext
                    if (fileName.StartsWith(prefix, StringComparison.Ordinal))
                    {
                        result.Add(new RollFile(file, seq));
                    }
                }
            }

            return result;
        }

        private string BuildRolledFilePath(int sequence)
        {
            var dir = Path.GetDirectoryName(_filePath);
            var name = Path.GetFileNameWithoutExtension(_filePath);
            var ext = Path.GetExtension(_filePath);
            var fileName = $"{name}.{sequence}{ext}";
            return string.IsNullOrEmpty(dir) ? fileName : Path.Combine(dir, fileName);
        }

        private string FormatLogEntry(LogEntry entry)
        {
            var formattedMessage = LogTemplateFormatter.ApplyTimestampTemplate(_config, entry.Timestamp)
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

        /// <summary>
        /// 刷盘：将 <see cref="StreamWriter"/> 缓冲写入底层流并 flush。
        /// </summary>
        private async Task FlushCoreAsync(CancellationToken cancellationToken)
        {
            if (_streamWriter is null) return;
            await _streamWriter.FlushAsync(cancellationToken).ConfigureAwait(false);
            if (_fileStream is not null)
            {
                await _fileStream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        public async Task FlushAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed) return;
            await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await FlushCoreAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task CloseAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed) return;
            await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await FlushCoreAsync(CancellationToken.None).ConfigureAwait(false);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            try
            {
                _streamWriter?.Flush();
            }
            catch { }

            _streamWriter?.Dispose();
            _fileStream?.Dispose();
            _semaphore.Dispose();
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

    /// <summary>
    /// 作用域日志记录器包装器。复用父 <see cref="AdvancedLogger"/> 的后台队列与写入器，
    /// 仅以独立的源名称与上下文属性区分作用域。
    /// 释放时仅取消订阅事件、清空本作用域的上下文属性——不会停止父记录器的后台队列。
    /// </summary>
    public sealed class ScopedLogger : ILogger
    {
        private readonly AdvancedLogger _parent;
        private readonly string _source;
        private readonly ConcurrentDictionary<string, object> _scopeProperties = new();
        private bool _disposed;

        public ScopedLogger(AdvancedLogger parent, string source, Dictionary<string, object>? scopeProperties)
        {
            _parent = parent ?? throw new ArgumentNullException(nameof(parent));
            _source = source;

            if (scopeProperties != null)
            {
                foreach (var property in scopeProperties)
                {
                    _scopeProperties[property.Key] = property.Value;
                }
            }
        }

        public void Debug(string message, string? source = null, Dictionary<string, object>? properties = null)
            => Enqueue(LogLevel.Debug, LogCategory.System, message, null, source, properties);

        public void Info(string message, string? source = null, Dictionary<string, object>? properties = null)
            => Enqueue(LogLevel.Info, LogCategory.System, message, null, source, properties);

        public void Warning(string message, string? source = null, Dictionary<string, object>? properties = null)
            => Enqueue(LogLevel.Warning, LogCategory.System, message, null, source, properties);

        public void Error(string message, Exception? exception = null, string? source = null, Dictionary<string, object>? properties = null)
            => Enqueue(LogLevel.Error, LogCategory.System, message, exception, source, properties);

        public void Critical(string message, Exception? exception = null, string? source = null, Dictionary<string, object>? properties = null)
            => Enqueue(LogLevel.Critical, LogCategory.System, message, exception, source, properties);

        public void Log(LogLevel level, LogCategory category, string message, Exception? exception = null, string? source = null, Dictionary<string, object>? properties = null)
            => Enqueue(level, category, message, exception, source, properties);

        /// <summary>
        /// 简化日志重载：仅按级别入队到父记录器。
        /// </summary>
        public void Log(LogLevel level, string message, string? source = null, Exception? exception = null)
            => Enqueue(level, LogCategory.System, message, exception, source, null);

        public Task DebugAsync(string message, string? source = null, Dictionary<string, object>? properties = null, CancellationToken cancellationToken = default)
        {
            Enqueue(LogLevel.Debug, LogCategory.System, message, null, source, properties);
            return Task.CompletedTask;
        }

        public Task InfoAsync(string message, string? source = null, Dictionary<string, object>? properties = null, CancellationToken cancellationToken = default)
        {
            Enqueue(LogLevel.Info, LogCategory.System, message, null, source, properties);
            return Task.CompletedTask;
        }

        public Task WarningAsync(string message, string? source = null, Dictionary<string, object>? properties = null, CancellationToken cancellationToken = default)
        {
            Enqueue(LogLevel.Warning, LogCategory.System, message, null, source, properties);
            return Task.CompletedTask;
        }

        public Task ErrorAsync(string message, Exception? exception = null, string? source = null, Dictionary<string, object>? properties = null, CancellationToken cancellationToken = default)
        {
            Enqueue(LogLevel.Error, LogCategory.System, message, exception, source, properties);
            return Task.CompletedTask;
        }

        public Task CriticalAsync(string message, Exception? exception = null, string? source = null, Dictionary<string, object>? properties = null, CancellationToken cancellationToken = default)
        {
            Enqueue(LogLevel.Critical, LogCategory.System, message, exception, source, properties);
            return Task.CompletedTask;
        }

        public Task LogAsync(LogLevel level, LogCategory category, string message, Exception? exception = null, string? source = null, Dictionary<string, object>? properties = null, CancellationToken cancellationToken = default)
        {
            Enqueue(level, category, message, exception, source, properties);
            return Task.CompletedTask;
        }

        public ILogger CreateScopedLogger(string scope, Dictionary<string, object>? scopeProperties = null)
        {
            var nestedSource = $"{_source}.{scope}";
            return new ScopedLogger(_parent, nestedSource, MergeScope(scopeProperties));
        }

        public void SetContextProperty(string key, object value)
        {
            _scopeProperties[key] = value;
        }

        public void RemoveContextProperty(string key)
        {
            _scopeProperties.TryRemove(key, out _);
        }

        public void ClearContextProperties()
        {
            _scopeProperties.Clear();
        }

        /// <summary>
        /// 释放作用域包装器。仅清空本作用域上下文属性——
        /// 不会停止父记录器的后台队列或释放写入器。
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _scopeProperties.Clear();
            GC.SuppressFinalize(this);
        }

        private void Enqueue(LogLevel level, LogCategory category, string message, Exception? exception, string? source, Dictionary<string, object>? properties)
        {
            if (_disposed || _parent.IsDisposed) return;

            // 合并作用域上下文属性与调用方属性
            var merged = properties is null && _scopeProperties.IsEmpty
                ? null
                : MergeProperties(properties);

            _parent.EnqueueScoped(level, category, message, exception, source ?? _source, merged);
        }

        private Dictionary<string, object>? MergeProperties(Dictionary<string, object>? properties)
        {
            if (_scopeProperties.IsEmpty && properties is null) return null;
            var result = new Dictionary<string, object>();
            foreach (var property in _scopeProperties)
            {
                result[property.Key] = property.Value;
            }
            if (properties != null)
            {
                foreach (var property in properties)
                {
                    result[property.Key] = property.Value;
                }
            }
            return result;
        }

        private Dictionary<string, object>? MergeScope(Dictionary<string, object>? additional)
        {
            if (additional is null || additional.Count == 0) return null;
            var result = new Dictionary<string, object>();
            foreach (var property in _scopeProperties)
            {
                result[property.Key] = property.Value;
            }
            foreach (var property in additional)
            {
                result[property.Key] = property.Value;
            }
            return result.Count == 0 ? null : result;
        }
    }

    /// <summary>
    /// 日志模板格式化辅助工具。将 <c>{Timestamp:format}</c> 占位符替换为
    /// 按指定格式化的时间戳，支持任意自定义格式字符串。
    /// </summary>
    internal static class LogTemplateFormatter
    {
        // 匹配 {Timestamp:format} —— 捕获整段占位符与格式说明
        private static readonly Regex TimestampRegex = new(
            @"\{Timestamp:([^}]+)\}",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        /// <summary>
        /// 将模板中所有 <c>{Timestamp:format}</c> 占位符替换为
        /// <paramref name="timestamp"/> 按对应格式说明的字符串输出。
        /// </summary>
        public static string ApplyTimestampTemplate(string template, DateTime timestamp)
        {
            if (string.IsNullOrEmpty(template)) return string.Empty;

            return TimestampRegex.Replace(template, match =>
            {
                var format = match.Groups[1].Value;
                try
                {
                    return timestamp.ToString(format);
                }
                catch (FormatException)
                {
                    // 非法格式说明时回退为通用可排序格式
                    return timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff");
                }
            });
        }

        /// <summary>
        /// 供写入器调用的便捷入口。
        /// </summary>
        public static string ApplyTimestampTemplate(LogConfiguration config, DateTime timestamp)
            => ApplyTimestampTemplate(config.FormatTemplate, timestamp);
    }
}
