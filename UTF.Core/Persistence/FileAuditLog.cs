using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace UTF.Core.Persistence;

public class FileAuditLog : IConfigurationAuditLog
{
    private readonly string _logPath;

    public FileAuditLog(string logPath = "logs/audit.json")
    {
        _logPath = logPath;
        Directory.CreateDirectory(Path.GetDirectoryName(_logPath)!);
    }

    public async Task LogChangeAsync(string configPath, string oldValue, string newValue, string user, CancellationToken ct = default)
    {
        var entry = new AuditEntry(Guid.NewGuid().ToString(), configPath, oldValue, newValue, user, DateTime.UtcNow);
        var entries = await LoadEntriesAsync(ct);
        entries.Add(entry);
        await File.WriteAllTextAsync(_logPath, JsonSerializer.Serialize(entries), ct);
    }

    public async Task<IEnumerable<AuditEntry>> GetLogsAsync(string? configPath = null, CancellationToken ct = default)
    {
        var entries = await LoadEntriesAsync(ct);
        return configPath == null ? entries : entries.Where(e => e.ConfigPath == configPath);
    }

    private async Task<List<AuditEntry>> LoadEntriesAsync(CancellationToken ct)
    {
        if (!File.Exists(_logPath)) return new();
        var json = await File.ReadAllTextAsync(_logPath, ct);
        return JsonSerializer.Deserialize<List<AuditEntry>>(json) ?? new();
    }
}
