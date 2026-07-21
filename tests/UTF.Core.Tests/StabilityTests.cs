using System.Diagnostics;
using UTF.Core;
using UTF.Core.Configuration;
using UTF.Core.Persistence;
using UTF.Logging;
using UTF.Plugin.Host;
using Xunit;

namespace UTF.Core.Tests;

/// <summary>
/// Phase C stability suite: multi-DUT parallel mock, cancellation, plugin host unload.
/// Uses MockOutput only (no real serial ports). Target: finish well under 30s total.
/// </summary>
public sealed class StabilityTests : IDisposable
{
    private readonly ILogger _logger;
    private readonly List<string> _tempDirectories = new();

    public StabilityTests()
    {
        Environment.SetEnvironmentVariable("UTFF_ALLOW_UNSIGNED_PLUGINS", "1");
        _logger = LoggerFactory.CreateLogger(
            nameof(StabilityTests),
            new LogConfiguration
            {
                EnableConsole = false,
                EnableFile = false
            });
    }

    public void Dispose()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        foreach (var directory in _tempDirectories)
        {
            try
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, recursive: true);
                }
            }
            catch
            {
                // Best-effort temp cleanup.
            }
        }

        if (_logger is IDisposable disposable)
        {
            disposable.Dispose();
        }

        Environment.SetEnvironmentVariable("UTFF_ALLOW_UNSIGNED_PLUGINS", null);
    }

    [Fact]
    [Trait("Category", "Stability")]
    public async Task MultiDutParallel_MockOutput_AllComplete()
    {
        const int dutCount = 24;
        var project = CreateMockProject(stepDelayMs: 0, steps: 3);
        var dutIds = Enumerable.Range(1, dutCount).Select(i => $"DUT-{i}").ToList();

        using var engine = new ConfigDrivenTestEngine(logger: _logger);
        using var orchestrator = new ConfigDrivenTestOrchestrator(
            new NullConfigurationService(),
            engine,
            _logger);

        var sw = Stopwatch.StartNew();
        var session = await orchestrator.CreateSessionAsync(
            project,
            dutIds,
            operatorName: "stability",
            dutConfig: new DUTConfigInfo { DefaultMaxConcurrent = 16 });

        Assert.NotNull(session);
        Assert.True(await orchestrator.StartSessionAsync(session!.SessionId));

        await orchestrator.WaitForSessionAsync(session.SessionId)
            .WaitAsync(TimeSpan.FromSeconds(20));

        sw.Stop();

        var final = orchestrator.GetSession(session.SessionId);
        Assert.NotNull(final);
        Assert.Equal(ConfigTestStatus.Completed, final!.Status);
        Assert.Equal(dutCount, final.DutResults.Count);
        Assert.All(final.DutResults.Values, r => Assert.True(r.Passed));
        Assert.True(final.OverallPassed);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(20), $"Elapsed {sw.Elapsed}");

        await orchestrator.CleanupSessionAsync(session.SessionId);
    }

    [Fact]
    [Trait("Category", "Stability")]
    public async Task SessionCancellation_MidRun_StopsWithoutHang()
    {
        // Long enough steps that cancel mid-run is observable; short enough for CI.
        var project = CreateMockProject(stepDelayMs: 200, steps: 8);
        var dutIds = Enumerable.Range(1, 16).Select(i => $"DUT-{i}").ToList();

        using var engine = new ConfigDrivenTestEngine(logger: _logger);
        using var orchestrator = new ConfigDrivenTestOrchestrator(
            new NullConfigurationService(),
            engine,
            _logger);

        var session = await orchestrator.CreateSessionAsync(
            project,
            dutIds,
            operatorName: "cancel-test",
            dutConfig: new DUTConfigInfo { DefaultMaxConcurrent = 8 });

        Assert.NotNull(session);
        Assert.True(await orchestrator.StartSessionAsync(session!.SessionId));

        // Let some work start, then cancel.
        await Task.Delay(150);

        var sw = Stopwatch.StartNew();
        var stopped = await orchestrator.StopSessionAsync(session.SessionId)
            .WaitAsync(TimeSpan.FromSeconds(15));
        sw.Stop();

        Assert.True(stopped);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(15), $"Stop hung: {sw.Elapsed}");

        var final = orchestrator.GetSession(session.SessionId);
        Assert.NotNull(final);
        Assert.True(
            final!.Status is ConfigTestStatus.Stopped or ConfigTestStatus.Completed or ConfigTestStatus.Error,
            $"Unexpected status: {final.Status}");
        Assert.False(final.OverallPassed);

        await orchestrator.CleanupSessionAsync(session.SessionId);
    }

    [Fact]
    [Trait("Category", "Stability")]
    public async Task PluginHost_LoadAndUnload_NoCrash()
    {
        var pluginRoot = StepExecutorPluginHostTestAssets.CreateTempDirectory(_tempDirectories);
        StepExecutorPluginHostTestAssets.CopyTestPluginPackage(
            pluginRoot,
            "priority.low",
            typeof(LowPriorityPassingPlugin).FullName!,
            priority: 10);

        await using var host = new StepExecutorPluginHost(pluginRoot, _logger);
        var report = await host.InitializeAsync();

        Assert.True(report.LoadedCount >= 1, $"Expected loaded plugin, failed={report.FailedCount}");
        Assert.Contains(host.LoadedPlugins, p => p.PluginId == "priority.low");

        var unloaded = await host.UnloadPluginAsync("priority.low");
        Assert.True(unloaded);
        Assert.DoesNotContain(host.LoadedPlugins, p => p.PluginId == "priority.low");

        // Second unload should be false, not throw.
        Assert.False(await host.UnloadPluginAsync("priority.low"));
    }

    [Fact]
    [Trait("Category", "Stability")]
    public async Task Engine_WithResultRepository_PersistsReports()
    {
        var resultsDir = StepExecutorPluginHostTestAssets.CreateTempDirectory(_tempDirectories);
        var repository = new FileTestResultRepository(resultsDir);
        using var engine = new ConfigDrivenTestEngine(
            logger: _logger,
            resultRepository: repository);

        var project = CreateMockProject(stepDelayMs: 0, steps: 1);
        var report = await engine.ExecuteTestProjectAsync(project, "DUT-1");

        Assert.True(report.Passed);
        var files = Directory.GetFiles(resultsDir, "*.json");
        Assert.NotEmpty(files);

        var loaded = await repository.QueryAsync(new TestResultQuery(DutId: "DUT-1"));
        Assert.NotEmpty(loaded);
    }

    [Fact]
    [Trait("Category", "Stability")]
    public async Task FileUnifiedConfigurationService_LoadsUnifiedConfigShape()
    {
        var dir = StepExecutorPluginHostTestAssets.CreateTempDirectory(_tempDirectories);
        var path = Path.Combine(dir, "unified-config.json");
        await File.WriteAllTextAsync(path, """
            {
              "SystemSettings": { "ResultsPath": "./out" },
              "DUTConfiguration": {
                "ProductInfo": { "Name": "Widget", "Model": "W1" },
                "GlobalSettings": { "DefaultMaxConcurrent": 8 }
              },
              "TestProjectConfiguration": {
                "TestProject": {
                  "Id": "proj-1",
                  "Name": "Mock",
                  "Enabled": true,
                  "Steps": [
                    {
                      "Id": "s1",
                      "Name": "mock",
                      "Order": 1,
                      "Type": "custom",
                      "Channel": "cmd",
                      "Command": "echo",
                      "Expected": "contains:OK",
                      "Parameters": { "MockOutput": "OK" }
                    }
                  ]
                }
              }
            }
            """);

        var service = new FileUnifiedConfigurationService(path);
        await service.RefreshAsync();

        var project = service.ToConfigTestProject();
        Assert.NotNull(project);
        Assert.Equal("proj-1", project!.Id);
        Assert.Single(project.Steps!);
        Assert.Equal("OK", project.Steps![0].Parameters!["MockOutput"].ToString());

        var dut = service.ToDutConfigInfo();
        Assert.Equal(8, dut.DefaultMaxConcurrent);
        Assert.Equal("Widget", dut.ProductName);

        using var engine = new ConfigDrivenTestEngine(logger: _logger);
        using var orchestrator = new ConfigDrivenTestOrchestrator(service, engine, _logger);
        var session = await orchestrator.CreateSessionAsync(project, new List<string> { "DUT-1" }, dutConfig: dut);
        Assert.NotNull(session);
    }

    private static ConfigTestProject CreateMockProject(int stepDelayMs, int steps)
    {
        var stepList = new List<ConfigTestStep>();
        for (var i = 1; i <= steps; i++)
        {
            stepList.Add(new ConfigTestStep
            {
                Id = $"step-{i}",
                Name = $"Mock Step {i}",
                Order = i,
                Enabled = true,
                Type = "custom",
                Channel = "cmd",
                Command = "mock",
                Expected = "contains:PASS",
                Delay = stepDelayMs > 0 ? stepDelayMs : null,
                RetryCount = 0,
                Parameters = new Dictionary<string, object>
                {
                    ["MockOutput"] = "PASS"
                }
            });
        }

        return new ConfigTestProject
        {
            Id = "stability_mock",
            Name = "Stability Mock Project",
            Description = "Phase C stability fixture",
            Enabled = true,
            Steps = stepList
        };
    }

    /// <summary>
    /// Minimal IConfigurationService for tests that use the ConfigTestProject overload.
    /// </summary>
    private sealed class NullConfigurationService : IConfigurationService
    {
        public event EventHandler? ConfigurationChanged
        {
            add { }
            remove { }
        }

        public Task<T?> GetConfigurationSectionAsync<T>(string section) where T : class =>
            Task.FromResult<T?>(null);

        public Task SaveConfigurationAsync(object config) => Task.CompletedTask;

        public Task RefreshAsync() => Task.CompletedTask;
    }
}
