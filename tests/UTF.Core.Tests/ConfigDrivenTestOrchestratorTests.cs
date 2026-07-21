using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UTF.Core;
using UTF.Core.Mapping;
using UTF.Logging;
using Xunit;

namespace UTF.Core.Tests;

/// <summary>
/// Tests for typed CreateSession overload and orchestrated multi-DUT run.
/// </summary>
public sealed class ConfigDrivenTestOrchestratorTests
{
    private readonly ILogger _logger = LoggerFactory.CreateLogger("OrchestratorTests");

    [Fact]
    [Trait("Category", "Unit")]
    public async Task CreateSessionAsync_TypedProject_CreatesSessionWithConcurrency()
    {
        var engine = new ConfigDrivenTestEngine(logger: _logger);
        var config = new FakeConfigurationService();
        using var orchestrator = new ConfigDrivenTestOrchestrator(config, engine, _logger);

        var project = TestProjectMapper.BuildProject(
            "proj-1",
            "Typed Project",
            "desc",
            enabled: true,
            steps: new[]
            {
                new ConfigTestStep
                {
                    Id = "s1",
                    Name = "Echo",
                    Order = 1,
                    Enabled = true,
                    Type = "custom",
                    Channel = "mock",
                    Command = "echo ok",
                    Expected = "contains:ok",
                    Timeout = 5000,
                    Parameters = new Dictionary<string, object> { ["MockOutput"] = "ok" }
                }
            });

        var dutConfig = TestProjectMapper.BuildDutConfig(defaultMaxConcurrent: 4, retryCount: 1);
        var session = await orchestrator.CreateSessionAsync(
            project,
            new[] { "DUT-1", "DUT-2" },
            operatorName: "tester",
            dutConfig: dutConfig);

        Assert.NotNull(session);
        Assert.Equal(ConfigTestStatus.Created, session!.Status);
        Assert.Equal(2, session.DutIds.Count);
        Assert.Equal(4, session.DutConfig!.DefaultMaxConcurrent);
        Assert.Equal("proj-1", session.TestProject.Id);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task CreateSessionAsync_DisabledProject_ReturnsNull()
    {
        var engine = new ConfigDrivenTestEngine(logger: _logger);
        using var orchestrator = new ConfigDrivenTestOrchestrator(new FakeConfigurationService(), engine, _logger);

        var project = new ConfigTestProject
        {
            Id = "disabled",
            Name = "Disabled",
            Enabled = false,
            Steps = new List<ConfigTestStep>
            {
                new()
                {
                    Id = "s1",
                    Name = "x",
                    Order = 1,
                    Enabled = true,
                    Type = "custom",
                    Channel = "mock",
                    Command = "echo",
                    Timeout = 1000
                }
            }
        };

        var session = await orchestrator.CreateSessionAsync(project, new[] { "DUT-1" });
        Assert.Null(session);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task StartSessionAsync_RunsAllDuts_AndFiresEvents()
    {
        var engine = new ConfigDrivenTestEngine(logger: _logger);
        using var orchestrator = new ConfigDrivenTestOrchestrator(new FakeConfigurationService(), engine, _logger);

        var project = TestProjectMapper.BuildProject(
            "proj-run",
            "Run Project",
            null,
            true,
            new[]
            {
                new ConfigTestStep
                {
                    Id = "step-1",
                    Name = "MockPass",
                    Order = 1,
                    Enabled = true,
                    Type = "custom",
                    Channel = "mock",
                    Command = "echo pass",
                    Expected = "contains:pass",
                    Timeout = 5000,
                    Parameters = new Dictionary<string, object> { ["MockOutput"] = "pass" }
                }
            });

        var perDut = new Dictionary<string, Dictionary<string, object>>(StringComparer.OrdinalIgnoreCase)
        {
            ["DUT-A"] = TestProjectMapper.BuildDutContext("DUT-A", "A", "type", "COM3", "10.0.0.1"),
            ["DUT-B"] = TestProjectMapper.BuildDutContext("DUT-B", "B", "type", "COM4", "10.0.0.2")
        };

        var session = await orchestrator.CreateSessionAsync(
            project,
            new[] { "DUT-A", "DUT-B" },
            dutConfig: TestProjectMapper.BuildDutConfig(defaultMaxConcurrent: 2),
            perDutContexts: perDut);

        Assert.NotNull(session);

        var dutStarted = 0;
        var dutCompleted = 0;
        var sessionCompleted = 0;
        orchestrator.DutStarted += (_, _) => Interlocked.Increment(ref dutStarted);
        orchestrator.DutCompleted += (_, _) => Interlocked.Increment(ref dutCompleted);
        orchestrator.SessionCompleted += (_, _) => Interlocked.Increment(ref sessionCompleted);

        var started = await orchestrator.StartSessionAsync(session!.SessionId);
        Assert.True(started);

        await orchestrator.WaitForSessionAsync(session.SessionId);

        var final = orchestrator.GetSession(session.SessionId);
        Assert.NotNull(final);
        Assert.Equal(ConfigTestStatus.Completed, final!.Status);
        Assert.True(final.OverallPassed);
        Assert.Equal(2, final.DutResults.Count);
        Assert.Equal(2, dutStarted);
        Assert.Equal(2, dutCompleted);
        Assert.Equal(1, sessionCompleted);

        var stats = orchestrator.GetSessionStatistics(session.SessionId);
        Assert.NotNull(stats);
        Assert.Equal(2, stats!.PassedDuts);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task CreateSessionAsync_ConfigServicePath_UsesTypedDtoMapping()
    {
        var engine = new ConfigDrivenTestEngine(logger: _logger);
        var config = new FakeConfigurationService
        {
            TestProjectSection = new OrchestratorTestProjectSection
            {
                TestProject = new OrchestratorTestProjectData
                {
                    Id = "from-config",
                    Name = "Config Project",
                    Enabled = true,
                    Steps = new List<OrchestratorStepData>
                    {
                        new()
                        {
                            Id = "s1",
                            Name = "Step",
                            Order = 1,
                            Enabled = true,
                            Type = "custom",
                            Channel = "mock",
                            Command = "echo",
                            Timeout = 1000,
                            Expected = "contains:x",
                            Parameters = new Dictionary<string, object> { ["MockOutput"] = "x" }
                        }
                    }
                }
            },
            DutSection = new OrchestratorDutSection
            {
                GlobalSettings = new OrchestratorGlobalSettings { DefaultMaxConcurrent = 8, RetryCount = 2 },
                CommunicationEndpoints = new OrchestratorCommunicationEndpoints
                {
                    SerialPorts = new List<string> { "COM1" },
                    NetworkHosts = new List<string> { "192.168.0.1" }
                }
            }
        };

        using var orchestrator = new ConfigDrivenTestOrchestrator(config, engine, _logger);
        var session = await orchestrator.CreateSessionAsync(new List<string> { "DUT-1" }, "op");
        Assert.NotNull(session);
        Assert.Equal("from-config", session!.TestProject.Id);
        Assert.Equal(8, session.DutConfig!.DefaultMaxConcurrent);
        Assert.Contains("COM1", session.DutConfig.SerialPorts);
    }

    private sealed class FakeConfigurationService : IConfigurationService
    {
        public OrchestratorTestProjectSection? TestProjectSection { get; set; }
        public OrchestratorDutSection? DutSection { get; set; }

#pragma warning disable CS0067 // Event required by interface; not raised in unit test fake
        public event EventHandler? ConfigurationChanged;
#pragma warning restore CS0067

        public Task<T?> GetConfigurationSectionAsync<T>(string section) where T : class
        {
            object? value = section switch
            {
                "TestProjectConfiguration" => TestProjectSection,
                "DUTConfiguration" => DutSection,
                _ => null
            };

            return Task.FromResult(value as T);
        }

        public Task SaveConfigurationAsync(object config) => Task.CompletedTask;
        public Task RefreshAsync() => Task.CompletedTask;
    }
}
