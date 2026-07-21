using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace UTF.Core.Persistence;

/// <summary>
/// 基于文件的配置审计日志 - 通过 SemaphoreSlim 串行化 load-modify-write 以避免并发覆盖
/// </summary>
public sealed class FileAuditLog : IConfigurationAuditLog, IDisposable
{
    private readonly string _logPath;
    private readonly SemaphoreSlim _fileLock = new(1, 1);
    private bool _disposed;

    public FileAuditLog(string logPath = "logs/audit.json")
    {
        _logPath = logPath;
        EnsureDirectory();
    }

    /// <summary>
    /// 释放内部信号量资源。
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _fileLock.Dispose();
        GC.SuppressFinalize(this);
    }

    public async Task LogChangeAsync(string configPath, string oldValue, string newValue, string user, CancellationToken ct = default)
    {
        await _fileLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var entry = new AuditEntry(Guid.NewGuid().ToString(), configPath, oldValue, newValue, user, DateTime.UtcNow);
            var entries = await LoadEntriesAsync(ct).ConfigureAwait(false);
            entries.Add(entry);
            EnsureDirectory();
            var temporaryPath = $"{_logPath}.{Guid.NewGuid():N}.tmp";
            try
            {
                await File.WriteAllTextAsync(temporaryPath, JsonSerializer.Serialize(entries), ct).ConfigureAwait(false);
                File.Move(temporaryPath, _logPath, overwrite: true);
            }
            finally
            {
                try
                {
                    if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
                }
                catch
                {
                    // Do not mask the original audit write failure.
                }
            }
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task<IEnumerable<AuditEntry>> GetLogsAsync(string? configPath = null, CancellationToken ct = default)
    {
        await _fileLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var entries = await LoadEntriesAsync(ct).ConfigureAwait(false);
            return configPath == null ? entries : entries.Where(e => e.ConfigPath == configPath);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    private async Task<List<AuditEntry>> LoadEntriesAsync(CancellationToken ct)
    {
        if (!File.Exists(_logPath)) return new();
        var json = await File.ReadAllTextAsync(_logPath, ct).ConfigureAwait(false);
        return JsonSerializer.Deserialize<List<AuditEntry>>(json) ?? new();
    }

    private void EnsureDirectory()
    {
        var dir = Path.GetDirectoryName(_logPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }
    }
}
