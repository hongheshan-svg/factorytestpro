using System.Collections.Generic;

namespace UTF.UI.Models;

/// <summary>
/// Catalog entry for a process/product template pack under <c>config/templates</c>.
/// </summary>
public sealed class TemplatePackInfo
{
    /// <summary>File name only (e.g. <c>factory-quick-start-minimal.json</c>).</summary>
    public string FileName { get; init; } = string.Empty;

    /// <summary>Absolute path to the template JSON file.</summary>
    public string FullPath { get; init; } = string.Empty;

    /// <summary>Display name from <c>ConfigurationInfo.Name</c>, falling back to file name.</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>Description from <c>ConfigurationInfo.Description</c>.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Industry inferred from file name (e.g. automotive, consumer-electronics).</summary>
    public string? Industry { get; init; }

    /// <summary>Tags inferred from file name segments.</summary>
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();

    /// <summary>Product name from <c>DUTConfiguration.ProductInfo.Name</c> (preview).</summary>
    public string? ProductName { get; init; }

    /// <summary>Product model from <c>DUTConfiguration.ProductInfo.Model</c> (preview).</summary>
    public string? ProductModel { get; init; }

    /// <summary>Number of steps in the template's test project (preview).</summary>
    public int StepCount { get; init; }

    /// <summary>One-line summary for list UI: industry + step count.</summary>
    public string SummaryLine
    {
        get
        {
            var industry = string.IsNullOrWhiteSpace(Industry) ? "通用" : Industry;
            return $"{industry} · {StepCount} 个步骤";
        }
    }
}
