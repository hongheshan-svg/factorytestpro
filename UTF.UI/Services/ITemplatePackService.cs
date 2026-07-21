using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UTF.UI.Models;

namespace UTF.UI.Services;

/// <summary>
/// Catalog and apply service for product/process template packs under <c>config/templates</c>.
/// </summary>
public interface ITemplatePackService
{
    /// <summary>Resolved templates directory (may be empty if none found).</summary>
    string TemplatesDirectory { get; }

    /// <summary>
    /// Scan the templates directory and return pack metadata for UI listing.
    /// Non-JSON or unreadable files are skipped.
    /// </summary>
    IReadOnlyList<TemplatePackInfo> GetAvailablePacks();

    /// <summary>Deserialize a template file into a <see cref="UnifiedConfiguration"/>.</summary>
    /// <param name="path">Absolute or relative path to a template JSON file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<UnifiedConfiguration> LoadPackAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Load a pack and save it as the active <c>unified-config.json</c> via
    /// <see cref="ConfigurationManager.SaveUnifiedConfigurationAsync"/>.
    /// Optionally backs up the previous unified-config first.
    /// </summary>
    /// <param name="path">Absolute or relative path to a template JSON file.</param>
    /// <param name="backupCurrent">When true, copy existing unified-config.json to a timestamped backup.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Path of the backup file if created; otherwise <c>null</c>.</returns>
    Task<string?> ApplyPackAsync(
        string path,
        bool backupCurrent = true,
        CancellationToken cancellationToken = default);
}
