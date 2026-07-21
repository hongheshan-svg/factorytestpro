using System;
using UTF.Plugin.Host;
using Xunit;

namespace UTF.Plugin.Host.Tests;

/// <summary>
/// Smoke tests for <see cref="StepExecutorPluginHost"/> construction and
/// empty-state behavior. The richer initialization/load/upgrade scenarios
/// already live in tests/UTF.Core.Tests/StepExecutorPluginHostTests.cs and
/// are not duplicated here.
/// TODO: migrate richer tests from UTF.Core.Tests later.
/// </summary>
public class PluginHostSmokeTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Constructor_NullPluginRoot_ThrowsArgumentNullException()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentNullException>(() => new StepExecutorPluginHost(null!));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void LoadedPlugins_BeforeInitialize_ReturnsEmptyList()
    {
        // Arrange
        using var host = new StepExecutorPluginHost("/nonexistent/plugin/root");

        // Act
        var plugins = host.LoadedPlugins;

        // Assert
        Assert.NotNull(plugins);
        Assert.Empty(plugins);
    }
}
