using System;
using System.Collections.Generic;
using System.Linq;
using UTF.Configuration.Models;

namespace UTF.Configuration;

/// <summary>
/// Bidirectional sync between generalized <see cref="EndpointDefinition"/> lists and
/// legacy <see cref="CommunicationEndpoints.SerialPorts"/> / <see cref="CommunicationEndpoints.NetworkHosts"/>.
/// </summary>
public static class EndpointMapper
{
    /// <summary>Well-known endpoint kinds (UI ComboBox source).</summary>
    public static readonly string[] KnownKinds =
    {
        "serial", "network", "telnet", "adb", "scpi", "custom"
    };

    /// <summary>
    /// If <see cref="DUTConfiguration.Endpoints"/> is null/empty, synthesize entries from
    /// legacy SerialPorts + NetworkHosts and write them back onto the configuration.
    /// Returns the (possibly newly created) endpoints list; never null.
    /// </summary>
    public static List<EndpointDefinition> NormalizeEndpoints(UnifiedConfiguration? config)
    {
        if (config == null)
        {
            return new List<EndpointDefinition>();
        }

        config.DUTConfiguration ??= new DUTConfiguration();
        var dut = config.DUTConfiguration;

        if (dut.Endpoints is { Count: > 0 })
        {
            return dut.Endpoints;
        }

        var synthesized = new List<EndpointDefinition>();
        var serialPorts = dut.CommunicationEndpoints?.SerialPorts;
        if (serialPorts != null)
        {
            for (var i = 0; i < serialPorts.Count; i++)
            {
                var port = serialPorts[i]?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(port))
                {
                    continue;
                }

                synthesized.Add(new EndpointDefinition
                {
                    Id = $"serial-{i + 1}",
                    Kind = "serial",
                    Address = port,
                    DisplayName = port
                });
            }
        }

        var networkHosts = dut.CommunicationEndpoints?.NetworkHosts;
        if (networkHosts != null)
        {
            for (var i = 0; i < networkHosts.Count; i++)
            {
                var host = networkHosts[i]?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(host))
                {
                    continue;
                }

                synthesized.Add(new EndpointDefinition
                {
                    Id = $"network-{i + 1}",
                    Kind = "network",
                    Address = host,
                    DisplayName = host
                });
            }
        }

        dut.Endpoints = synthesized;
        return synthesized;
    }

    /// <summary>
    /// Mirror the Endpoints list back into legacy SerialPorts / NetworkHosts so consumers
    /// that still read only those lists (e.g. DUTMonitorManager index assignment) keep working.
    /// Serial-like kinds map to SerialPorts; network/telnet/adb map to NetworkHosts.
    /// </summary>
    public static void MirrorEndpointsToLegacy(UnifiedConfiguration? config)
    {
        if (config?.DUTConfiguration == null)
        {
            return;
        }

        var dut = config.DUTConfiguration;
        var endpoints = dut.Endpoints;
        if (endpoints == null || endpoints.Count == 0)
        {
            return;
        }

        dut.CommunicationEndpoints ??= new CommunicationEndpoints();

        dut.CommunicationEndpoints.SerialPorts = endpoints
            .Where(e => IsSerialLike(e.Kind) && !string.IsNullOrWhiteSpace(e.Address))
            .Select(e => e.Address.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        dut.CommunicationEndpoints.NetworkHosts = endpoints
            .Where(e => IsNetworkLike(e.Kind) && !string.IsNullOrWhiteSpace(e.Address))
            .Select(e => e.Address.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Serial-like addresses for legacy consumers. Prefers normalized Endpoints; falls back to legacy lists.
    /// </summary>
    public static List<string> GetSerialAddresses(UnifiedConfiguration? config)
    {
        var endpoints = NormalizeEndpoints(config);
        var fromEndpoints = endpoints
            .Where(e => IsSerialLike(e.Kind) && !string.IsNullOrWhiteSpace(e.Address))
            .Select(e => e.Address.Trim())
            .ToList();

        if (fromEndpoints.Count > 0)
        {
            return fromEndpoints;
        }

        return config?.DUTConfiguration?.CommunicationEndpoints?.SerialPorts
                ?.Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => p.Trim())
                .ToList()
            ?? new List<string>();
    }

    /// <summary>
    /// Network-like addresses for legacy consumers. Prefers normalized Endpoints; falls back to legacy lists.
    /// </summary>
    public static List<string> GetNetworkAddresses(UnifiedConfiguration? config)
    {
        var endpoints = NormalizeEndpoints(config);
        var fromEndpoints = endpoints
            .Where(e => IsNetworkLike(e.Kind) && !string.IsNullOrWhiteSpace(e.Address))
            .Select(e => e.Address.Trim())
            .ToList();

        if (fromEndpoints.Count > 0)
        {
            return fromEndpoints;
        }

        return config?.DUTConfiguration?.CommunicationEndpoints?.NetworkHosts
                ?.Where(h => !string.IsNullOrWhiteSpace(h))
                .Select(h => h.Trim())
                .ToList()
            ?? new List<string>();
    }

    /// <summary>
    /// Adds <c>Endpoint:{id}</c> keys (address values) into an existing context dictionary.
    /// Safe to call with null/empty endpoints (no-op).
    /// </summary>
    public static void AddEndpointContextKeys(
        IDictionary<string, object> context,
        IEnumerable<EndpointDefinition>? endpoints)
    {
        if (context == null || endpoints == null)
        {
            return;
        }

        foreach (var ep in endpoints)
        {
            if (string.IsNullOrWhiteSpace(ep.Id))
            {
                continue;
            }

            context[$"Endpoint:{ep.Id.Trim()}"] = ep.Address?.Trim() ?? string.Empty;
        }
    }

    /// <summary>Validation errors for the Endpoints list (unique Id, Kind, Address).</summary>
    public static List<string> ValidateEndpoints(IEnumerable<EndpointDefinition>? endpoints)
    {
        var errors = new List<string>();
        if (endpoints == null)
        {
            return errors;
        }

        var list = endpoints.ToList();
        if (list.Count == 0)
        {
            return errors;
        }

        var ids = new List<string>();
        for (var i = 0; i < list.Count; i++)
        {
            var ep = list[i];
            var label = $"端点{i + 1}";

            if (string.IsNullOrWhiteSpace(ep.Id))
            {
                errors.Add($"{label}: Id 不能为空");
            }
            else
            {
                ids.Add(ep.Id.Trim());
            }

            if (string.IsNullOrWhiteSpace(ep.Kind))
            {
                errors.Add($"{label}({ep.Id}): Kind 不能为空");
            }

            if (string.IsNullOrWhiteSpace(ep.Address))
            {
                errors.Add($"{label}({ep.Id}): Address 不能为空");
            }
        }

        var duplicateIds = ids
            .GroupBy(id => id, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        if (duplicateIds.Count > 0)
        {
            errors.Add($"端点 Id 重复: {string.Join(", ", duplicateIds)}");
        }

        return errors;
    }

    public static bool IsSerialLike(string? kind)
        => string.Equals(kind, "serial", StringComparison.OrdinalIgnoreCase)
           || string.Equals(kind, "uart", StringComparison.OrdinalIgnoreCase);

    public static bool IsNetworkLike(string? kind)
        => string.Equals(kind, "network", StringComparison.OrdinalIgnoreCase)
           || string.Equals(kind, "telnet", StringComparison.OrdinalIgnoreCase)
           || string.Equals(kind, "adb", StringComparison.OrdinalIgnoreCase)
           || string.Equals(kind, "scpi", StringComparison.OrdinalIgnoreCase);
}
