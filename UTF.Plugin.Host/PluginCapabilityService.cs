using System;
using System.Collections.Generic;
using System.Linq;
using UTF.Core;
using UTF.Plugin.Abstractions;

namespace UTF.Plugin.Host;

/// <summary>
/// Default <see cref="IPluginCapabilityService"/> over loaded plugin metadata.
/// </summary>
public sealed class PluginCapabilityService : IPluginCapabilityService
{
    private readonly Func<IReadOnlyList<PluginMetadata>> _pluginsProvider;

    public PluginCapabilityService(StepExecutorPluginHost pluginHost)
        : this(() => (pluginHost ?? throw new ArgumentNullException(nameof(pluginHost))).LoadedPlugins)
    {
    }

    /// <summary>
    /// Test-friendly constructor that supplies plugin metadata without a live host.
    /// </summary>
    public PluginCapabilityService(Func<IReadOnlyList<PluginMetadata>> pluginsProvider)
    {
        _pluginsProvider = pluginsProvider ?? throw new ArgumentNullException(nameof(pluginsProvider));
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetAllStepTypes()
        => CollectDistinct(_pluginsProvider().SelectMany(p => p.SupportedStepTypes));

    /// <inheritdoc />
    public IReadOnlyList<string> GetAllChannels()
        => CollectDistinct(_pluginsProvider().SelectMany(p => p.SupportedChannels));

    /// <inheritdoc />
    public IReadOnlyList<PluginParameterSchemaItem> GetParameterSchema(string? stepType, string? channel)
    {
        var match = _pluginsProvider()
            .Where(p => Matches(p.SupportedStepTypes, stepType) && Matches(p.SupportedChannels, channel))
            .OrderBy(p => p.Priority)
            .ThenBy(p => p.PluginId, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        if (match is null || match.ParameterSchema.Count == 0)
        {
            return Array.Empty<PluginParameterSchemaItem>();
        }

        return match.ParameterSchema;
    }

    private static IReadOnlyList<string> CollectDistinct(IEnumerable<string> values)
    {
        return values
            .Where(v => !string.IsNullOrWhiteSpace(v)
                        && !string.Equals(v, "*", StringComparison.OrdinalIgnoreCase))
            .Select(v => v.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(v => v, StringComparer.OrdinalIgnoreCase)
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// AND-compatible side match used by plugin routing: "*" on either side is a wildcard.
    /// Empty request value never matches a concrete supported list (only matches "*").
    /// </summary>
    public static bool Matches(IReadOnlyList<string> supported, string? value)
    {
        if (supported.Count == 0)
        {
            return false;
        }

        if (supported.Contains("*", StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (string.Equals(value, "*", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return supported.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase);
    }
}
