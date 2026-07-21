using System.Collections.Generic;
using System.Linq;
using UTF.Configuration;
using UTF.Configuration.Models;
using Xunit;

namespace UTF.Configuration.Tests;

/// <summary>
/// Unit tests for <see cref="EndpointMapper"/> normalize / mirror / validation helpers (P3).
/// </summary>
public class EndpointMapperTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void NormalizeEndpoints_WhenEmpty_SynthesizesFromLegacyLists()
    {
        var config = new UnifiedConfiguration
        {
            DUTConfiguration = new DUTConfiguration
            {
                CommunicationEndpoints = new CommunicationEndpoints
                {
                    SerialPorts = new List<string> { "COM3", "COM4" },
                    NetworkHosts = new List<string> { "192.168.1.10" }
                }
            }
        };

        var endpoints = EndpointMapper.NormalizeEndpoints(config);

        Assert.Equal(3, endpoints.Count);
        Assert.Contains(endpoints, e => e.Kind == "serial" && e.Address == "COM3" && e.Id == "serial-1");
        Assert.Contains(endpoints, e => e.Kind == "serial" && e.Address == "COM4" && e.Id == "serial-2");
        Assert.Contains(endpoints, e => e.Kind == "network" && e.Address == "192.168.1.10" && e.Id == "network-1");
        Assert.Same(endpoints, config.DUTConfiguration.Endpoints);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void NormalizeEndpoints_WhenPresent_DoesNotOverwrite()
    {
        var existing = new List<EndpointDefinition>
        {
            new() { Id = "adb-1", Kind = "adb", Address = "emulator-5554" }
        };
        var config = new UnifiedConfiguration
        {
            DUTConfiguration = new DUTConfiguration
            {
                Endpoints = existing,
                CommunicationEndpoints = new CommunicationEndpoints
                {
                    SerialPorts = new List<string> { "COM9" }
                }
            }
        };

        var endpoints = EndpointMapper.NormalizeEndpoints(config);

        Assert.Single(endpoints);
        Assert.Equal("adb-1", endpoints[0].Id);
        Assert.Same(existing, endpoints);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void MirrorEndpointsToLegacy_WritesSerialAndNetworkLists()
    {
        var config = new UnifiedConfiguration
        {
            DUTConfiguration = new DUTConfiguration
            {
                Endpoints = new List<EndpointDefinition>
                {
                    new() { Id = "serial-1", Kind = "serial", Address = "COM7" },
                    new() { Id = "telnet-1", Kind = "telnet", Address = "10.0.0.5:23" },
                    new() { Id = "custom-1", Kind = "custom", Address = "pipe://x" }
                }
            }
        };

        EndpointMapper.MirrorEndpointsToLegacy(config);

        Assert.Equal(new[] { "COM7" }, config.DUTConfiguration.CommunicationEndpoints!.SerialPorts);
        Assert.Equal(new[] { "10.0.0.5:23" }, config.DUTConfiguration.CommunicationEndpoints.NetworkHosts);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ValidateEndpoints_DuplicateId_Fails()
    {
        var errors = EndpointMapper.ValidateEndpoints(new[]
        {
            new EndpointDefinition { Id = "ep-1", Kind = "serial", Address = "COM3" },
            new EndpointDefinition { Id = "EP-1", Kind = "network", Address = "1.2.3.4" }
        });

        Assert.Contains(errors, e => e.Contains("重复"));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ValidateEndpoints_EmptyKindOrAddress_Fails()
    {
        var errors = EndpointMapper.ValidateEndpoints(new[]
        {
            new EndpointDefinition { Id = "ep-1", Kind = "", Address = "COM3" },
            new EndpointDefinition { Id = "ep-2", Kind = "serial", Address = "  " }
        });

        Assert.Contains(errors, e => e.Contains("Kind"));
        Assert.Contains(errors, e => e.Contains("Address"));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ValidateEndpoints_EmptyList_Succeeds()
    {
        var errors = EndpointMapper.ValidateEndpoints(new List<EndpointDefinition>());
        Assert.Empty(errors);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void AddEndpointContextKeys_InjectsEndpointIdKeys()
    {
        var context = new Dictionary<string, object>();
        EndpointMapper.AddEndpointContextKeys(context, new[]
        {
            new EndpointDefinition { Id = "serial-1", Kind = "serial", Address = "COM3" },
            new EndpointDefinition { Id = "  ", Kind = "serial", Address = "COM4" }
        });

        Assert.Equal("COM3", context["Endpoint:serial-1"]);
        Assert.Single(context);
        Assert.DoesNotContain(context.Keys, k => k.Contains("COM4", System.StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Adapter_GetSerialPorts_PrefersEndpoints()
    {
        var adapter = new UnifiedConfigurationAdapter();
        var config = new UnifiedConfiguration
        {
            DUTConfiguration = new DUTConfiguration
            {
                Endpoints = new List<EndpointDefinition>
                {
                    new() { Id = "s1", Kind = "serial", Address = "COM12" }
                },
                CommunicationEndpoints = new CommunicationEndpoints
                {
                    SerialPorts = new List<string> { "COM3" }
                }
            }
        };

        var ports = adapter.GetSerialPorts(config);
        Assert.Equal(new[] { "COM12" }, ports);
    }
}
