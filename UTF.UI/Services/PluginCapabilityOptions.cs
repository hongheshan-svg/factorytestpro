using System;
using System.Collections.Generic;
using System.Linq;
using UTF.Plugin.Abstractions;

namespace UTF.UI.Services;

/// <summary>
/// Merges step-type / channel options advertised by loaded plugins for UI dropdowns.
/// When no plugins are loaded, returns a small set of safe fallbacks so editors remain usable.
/// </summary>
public static class PluginCapabilityOptions
{
    /// <summary>Fallback step types when no plugin metadata is available.</summary>
    public static readonly string[] FallbackStepTypes = { "custom", "serial", "cmd" };

    /// <summary>Fallback channels when no plugin metadata is available.</summary>
    public static readonly string[] FallbackChannels = { "cmd", "serial" };

    /// <summary>
    /// Collect distinct, case-insensitive step types from <paramref name="plugins"/>,
    /// sorted for display. Falls back to <see cref="FallbackStepTypes"/> when empty.
    /// </summary>
    public static IReadOnlyList<string> MergeStepTypes(IEnumerable<PluginMetadata>? plugins)
        => Merge(plugins?.SelectMany(p => p.SupportedStepTypes ?? Array.Empty<string>()), FallbackStepTypes);

    /// <summary>
    /// Collect distinct, case-insensitive channels from <paramref name="plugins"/>,
    /// sorted for display. Falls back to <see cref="FallbackChannels"/> when empty.
    /// </summary>
    public static IReadOnlyList<string> MergeChannels(IEnumerable<PluginMetadata>? plugins)
        => Merge(plugins?.SelectMany(p => p.SupportedChannels ?? Array.Empty<string>()), FallbackChannels);

    private static IReadOnlyList<string> Merge(IEnumerable<string>? values, IReadOnlyList<string> fallbacks)
    {
        // OrdinalIgnoreCase keeps first casing; SortedSet yields sorted distinct results.
        var set = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        if (values != null)
        {
            foreach (var raw in values)
            {
                if (string.IsNullOrWhiteSpace(raw))
                {
                    continue;
                }

                set.Add(raw.Trim());
            }
        }

        if (set.Count == 0)
        {
            foreach (var fallback in fallbacks)
            {
                set.Add(fallback);
            }
        }

        return set.ToList();
    }
}
