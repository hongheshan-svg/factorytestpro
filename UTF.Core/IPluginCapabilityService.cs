using System.Collections.Generic;
using UTF.Plugin.Abstractions;

namespace UTF.Core;

/// <summary>
/// Queries loaded plugins for aggregate capabilities used by UI and tooling.
/// Shared API surface for Configuration Center type/channel dropdowns (P0)
/// and dynamic parameter forms (P4).
/// </summary>
public interface IPluginCapabilityService
{
    /// <summary>
    /// Distinct step types advertised by loaded plugins (case-insensitive union, sorted).
    /// Wildcard "*" is omitted.
    /// </summary>
    IReadOnlyList<string> GetAllStepTypes();

    /// <summary>
    /// Distinct channels advertised by loaded plugins (case-insensitive union, sorted).
    /// Wildcard "*" is omitted.
    /// </summary>
    IReadOnlyList<string> GetAllChannels();

    /// <summary>
    /// Parameter schema for the highest-priority plugin that can handle
    /// <paramref name="stepType"/> + <paramref name="channel"/> (lower Priority wins).
    /// Returns an empty list when no match or schema is absent.
    /// </summary>
    IReadOnlyList<PluginParameterSchemaItem> GetParameterSchema(string? stepType, string? channel);
}
