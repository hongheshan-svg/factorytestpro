using System.Threading;
using System.Threading.Tasks;

namespace UTF.Core.Persistence;

public interface IConfigurationAuditLog
{
    Task LogChangeAsync(string configPath, string oldValue, string newValue, string user, CancellationToken ct = default);
    Task<System.Collections.Generic.IEnumerable<AuditEntry>> GetLogsAsync(string? configPath = null, CancellationToken ct = default);
}

public record AuditEntry(
    string Id,
    string ConfigPath,
    string OldValue,
    string NewValue,
    string User,
    System.DateTime Timestamp
);
