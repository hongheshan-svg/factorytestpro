using System;
using System.IO;
using System.Threading.Tasks;
using UTF.Plugin.Abstractions;
using UTF.Plugin.Host;
using Xunit;

namespace UTF.Core.Tests;

public class StepExecutorPluginHostTests : IDisposable
{
    private readonly List<string> _tempDirectories = new();

    public void Dispose()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        foreach (var directory in _tempDirectories)
        {
            if (!Directory.Exists(directory))
            {
                continue;
            }

            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task InitializeAsync_NonExistentDirectory_ReturnsEmptyReport()
    {
        var host = new StepExecutorPluginHost(StepExecutorPluginHostTestAssets.CreateNonExistentPath());

        var report = await host.InitializeAsync();

        Assert.Equal(0, report.LoadedCount);
        Assert.Equal(0, report.FailedCount);
    }

    [Fact]
    public async Task InitializeAsync_EmptyDirectory_ReturnsEmptyReport()
    {
        var host = new StepExecutorPluginHost(StepExecutorPluginHostTestAssets.CreateTempDirectory(_tempDirectories));
        var report = await host.InitializeAsync();

        Assert.Equal(0, report.LoadedCount);
        Assert.Equal(0, report.FailedCount);
    }

    [Fact]
    public async Task InitializeAsync_CalledTwice_ReturnsExistingCount()
    {
        var host = new StepExecutorPluginHost(StepExecutorPluginHostTestAssets.CreateNonExistentPath());

        var first = await host.InitializeAsync();
        var second = await host.InitializeAsync();

        Assert.Equal(first.LoadedCount, second.LoadedCount);
    }

    [Fact]
    public async Task UnloadPluginAsync_NonExistentPlugin_ReturnsFalse()
    {
        var host = new StepExecutorPluginHost(StepExecutorPluginHostTestAssets.CreateNonExistentPath());
        await host.InitializeAsync();

        bool result = await host.UnloadPluginAsync("non.existent.plugin");

        Assert.False(result);
    }

    [Fact]
    public async Task UnloadPluginAsync_NullPluginId_ThrowsArgumentNullException()
    {
        var host = new StepExecutorPluginHost(StepExecutorPluginHostTestAssets.CreateNonExistentPath());

        await Assert.ThrowsAsync<ArgumentNullException>(() => host.UnloadPluginAsync(null!));
    }

    [Fact]
    public async Task ReloadPluginAsync_NonExistentPlugin_ReturnsFailure()
    {
        var host = new StepExecutorPluginHost(StepExecutorPluginHostTestAssets.CreateNonExistentPath());
        await host.InitializeAsync();

        var report = await host.ReloadPluginAsync("non.existent.plugin");

        Assert.Equal(0, report.LoadedCount);
        Assert.Equal(1, report.FailedCount);
        Assert.Single(report.Issues);
        Assert.Equal(PluginErrorCodes.PluginNotFound, report.Issues[0].ErrorCode);
    }

    [Fact]
    public async Task ReloadPluginAsync_NullPluginId_ThrowsArgumentNullException()
    {
        var host = new StepExecutorPluginHost(StepExecutorPluginHostTestAssets.CreateNonExistentPath());

        await Assert.ThrowsAsync<ArgumentNullException>(() => host.ReloadPluginAsync(null!));
    }

    [Fact]
    public async Task UpgradePluginAsync_NonExistentManifest_ReturnsFailure()
    {
        var host = new StepExecutorPluginHost(StepExecutorPluginHostTestAssets.CreateNonExistentPath());
        var manifestPath = Path.Combine(StepExecutorPluginHostTestAssets.CreateTempDirectory(_tempDirectories), "missing.manifest.json");

        var report = await host.UpgradePluginAsync("test.plugin", manifestPath);

        Assert.Equal(0, report.LoadedCount);
        Assert.Equal(1, report.FailedCount);
        Assert.Equal(PluginErrorCodes.ManifestInvalid, report.Issues[0].ErrorCode);
    }

    [Fact]
    public async Task UpgradePluginAsync_MismatchedPluginId_ReturnsUpgradeFailure()
    {
        var host = new StepExecutorPluginHost(StepExecutorPluginHostTestAssets.CreateNonExistentPath());
        var pluginDirectory = StepExecutorPluginHostTestAssets.CreateTempDirectory(_tempDirectories);
        var manifestPath = Path.Combine(pluginDirectory, "plugin.manifest.json");
        File.WriteAllText(
            manifestPath,
            """
            {
              "pluginId": "different.plugin",
              "version": "2.0.0",
              "pluginApiVersion": "1.0",
              "entryAssembly": "Different.Plugin.dll",
              "entryType": "Different.Plugin.Entry"
            }
            """);

        var report = await host.UpgradePluginAsync("target.plugin", manifestPath);

        Assert.Equal(0, report.LoadedCount);
        Assert.Equal(1, report.FailedCount);
        Assert.Equal(PluginErrorCodes.UpgradeFailed, report.Issues[0].ErrorCode);
    }

    [Fact]
    public async Task UpgradePluginAsync_NullPluginId_ThrowsArgumentNullException()
    {
        var host = new StepExecutorPluginHost(StepExecutorPluginHostTestAssets.CreateNonExistentPath());

        await Assert.ThrowsAsync<ArgumentNullException>(() => host.UpgradePluginAsync(null!, "manifest.json"));
    }

    [Fact]
    public async Task UpgradePluginAsync_NullManifestPath_ThrowsArgumentNullException()
    {
        var host = new StepExecutorPluginHost(StepExecutorPluginHostTestAssets.CreateNonExistentPath());

        await Assert.ThrowsAsync<ArgumentNullException>(() => host.UpgradePluginAsync("test.plugin", null!));
    }

    [Fact]
    public async Task ExecuteAsync_NoMatchingPlugin_ReturnsNoMatchingPluginError()
    {
        var host = new StepExecutorPluginHost(StepExecutorPluginHostTestAssets.CreateNonExistentPath());

        var result = await host.ExecuteAsync(new StepExecutionRequest
        {
            StepId = "step-1",
            StepName = "Missing Plugin Step",
            StepType = "custom",
            Channel = "Serial",
            Command = "noop"
        });

        Assert.Equal(StepExecutionStatus.Error, result.Status);
        Assert.Equal(PluginErrorCodes.NoMatchingPlugin, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_TwoRealManifestPluginsWithSamePriority_ReturnsConflictError()
    {
        var pluginRoot = StepExecutorPluginHostTestAssets.CreateTempDirectory(_tempDirectories);
        StepExecutorPluginHostTestAssets.CopyRealCmdPluginPackage(pluginRoot, "utf.executor.cmd.copyA");
        StepExecutorPluginHostTestAssets.CopyRealCmdPluginPackage(pluginRoot, "utf.executor.cmd.copyB");

        using var host = new StepExecutorPluginHost(pluginRoot);
        var report = await host.InitializeAsync();

        Assert.Equal(2, report.LoadedCount);
        Assert.Equal(0, report.FailedCount);

        var result = await host.ExecuteAsync(new StepExecutionRequest
        {
            StepId = "step-conflict",
            StepName = "Conflict Step",
            StepType = "custom",
            Channel = "cmd",
            Command = "echo hello"
        });

        Assert.Equal(StepExecutionStatus.Error, result.Status);
        Assert.Equal(PluginErrorCodes.MultipleMatchingPlugins, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_TwoManifestPluginsWithDifferentPriority_SelectsLowerPriorityValuePlugin()
    {
        var pluginRoot = StepExecutorPluginHostTestAssets.CreateTempDirectory(_tempDirectories);
        StepExecutorPluginHostTestAssets.CopyTestPluginPackage(
            pluginRoot,
            pluginDirectoryName: "priority.high",
            entryType: typeof(HighPriorityFailingPlugin).FullName!,
            priority: 200);
        StepExecutorPluginHostTestAssets.CopyTestPluginPackage(
            pluginRoot,
            pluginDirectoryName: "priority.low",
            entryType: typeof(LowPriorityPassingPlugin).FullName!,
            priority: 10);

        using var host = new StepExecutorPluginHost(pluginRoot);
        var report = await host.InitializeAsync();

        Assert.Equal(2, report.LoadedCount);
        Assert.Equal(0, report.FailedCount);

        var result = await host.ExecuteAsync(new StepExecutionRequest
        {
            StepId = "step-priority",
            StepName = "Priority Step",
            StepType = "custom",
            Channel = "cmd",
            Command = "ignored"
        });

        Assert.Equal(StepExecutionStatus.Passed, result.Status);
        Assert.Equal("priority.low", result.PluginId);
        Assert.Equal("selected-low-priority", result.NormalizedOutput);
    }

    [Fact]
    public async Task InitializeAsync_InvalidJsonManifest_ReturnsManifestInvalidIssue()
    {
        var pluginRoot = StepExecutorPluginHostTestAssets.CreateTempDirectory(_tempDirectories);
        StepExecutorPluginHostTestAssets.CreateInvalidJsonManifestPackage(pluginRoot, "invalid.json.plugin");

        using var host = new StepExecutorPluginHost(pluginRoot);
        var report = await host.InitializeAsync();

        Assert.Equal(0, report.LoadedCount);
        Assert.Equal(1, report.FailedCount);
        Assert.Single(report.Issues);
        Assert.Equal(PluginErrorCodes.ManifestInvalid, report.Issues[0].ErrorCode);
    }

    [Fact]
    public async Task InitializeAsync_IncompatibleApiVersion_ReturnsApiVersionIncompatibleIssue()
    {
        var pluginRoot = StepExecutorPluginHostTestAssets.CreateTempDirectory(_tempDirectories);
        StepExecutorPluginHostTestAssets.CopyTestPluginPackage(
            pluginRoot,
            pluginDirectoryName: "api.version.mismatch",
            entryType: typeof(LowPriorityPassingPlugin).FullName!,
            priority: 10,
            pluginApiVersion: "2.0");

        using var host = new StepExecutorPluginHost(pluginRoot);
        var report = await host.InitializeAsync();

        Assert.Equal(0, report.LoadedCount);
        Assert.Equal(1, report.FailedCount);
        Assert.Single(report.Issues);
        Assert.Equal(PluginErrorCodes.ApiVersionIncompatible, report.Issues[0].ErrorCode);
    }

    [Fact]
    public async Task InitializeAsync_InvalidSha256_ReturnsIntegrityCheckFailedIssue()
    {
        var pluginRoot = StepExecutorPluginHostTestAssets.CreateTempDirectory(_tempDirectories);
        StepExecutorPluginHostTestAssets.CopyTestPluginPackage(
            pluginRoot,
            pluginDirectoryName: "sha.mismatch.plugin",
            entryType: typeof(LowPriorityPassingPlugin).FullName!,
            priority: 10,
            sha256: "deadbeef");

        using var host = new StepExecutorPluginHost(pluginRoot);
        var report = await host.InitializeAsync();

        Assert.Equal(0, report.LoadedCount);
        Assert.Equal(1, report.FailedCount);
        Assert.Single(report.Issues);
        Assert.Equal(PluginErrorCodes.IntegrityCheckFailed, report.Issues[0].ErrorCode);
    }

    [Fact]
    public void HealthCheck_NoPlugins_ReturnsEmptyReport()
    {
        var host = new StepExecutorPluginHost(StepExecutorPluginHostTestAssets.CreateNonExistentPath());

        var health = host.HealthCheck();

        Assert.Equal(0, health.TotalPlugins);
        Assert.Equal(0, health.HealthyPlugins);
        Assert.Empty(health.Entries);
    }

    [Fact]
    public void Constructor_NullPluginRoot_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new StepExecutorPluginHost(null!));
    }

    [Fact]
    public void LoadedPlugins_BeforeInitialize_ReturnsEmptyList()
    {
        var host = new StepExecutorPluginHost(StepExecutorPluginHostTestAssets.CreateNonExistentPath());

        Assert.Empty(host.LoadedPlugins);
    }

    [Fact]
    public void Dispose_NoPlugins_DoesNotThrow()
    {
        var host = new StepExecutorPluginHost(StepExecutorPluginHostTestAssets.CreateNonExistentPath());

        host.Dispose();
    }
}
