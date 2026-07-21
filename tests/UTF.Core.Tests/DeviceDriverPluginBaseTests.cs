using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UTF.Plugin.Abstractions;
using Xunit;

namespace UTF.Core.Tests;

/// <summary>
/// Verifies per-endpoint isolation on <see cref="DeviceDriverPluginBase"/>.
/// </summary>
public sealed class DeviceDriverPluginBaseTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task ExecuteAsync_DifferentEndpoints_RunConcurrentlyWithoutReconnectThrash()
    {
        var driver = new FakeMultiEndpointDriver();
        await driver.InitializeAsync(new PluginInitContext
        {
            PluginApiVersion = PluginApiVersions.V1,
            FrameworkVersion = "test"
        });

        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        driver.BlockSendUntil = gate.Task;

        var t1 = driver.ExecuteAsync(CreateRequest("EP-A", "cmd-a"), CancellationToken.None);
        var t2 = driver.ExecuteAsync(CreateRequest("EP-B", "cmd-b"), CancellationToken.None);

        // Both endpoints should have opened a connection before either send completes.
        await WaitUntilAsync(() => driver.CreateCounts["EP-A"] == 1 && driver.CreateCounts["EP-B"] == 1);

        gate.SetResult();
        var r1 = await t1;
        var r2 = await t2;

        Assert.Equal(StepExecutionStatus.Passed, r1.Status);
        Assert.Equal(StepExecutionStatus.Passed, r2.Status);
        Assert.Equal(1, driver.CreateCounts["EP-A"]);
        Assert.Equal(1, driver.CreateCounts["EP-B"]);
        Assert.Equal(2, driver.ActiveConnections);

        // Second round should reuse connections (no extra Create).
        var r3 = await driver.ExecuteAsync(CreateRequest("EP-A", "cmd-a2"), CancellationToken.None);
        Assert.Equal(StepExecutionStatus.Passed, r3.Status);
        Assert.Equal(1, driver.CreateCounts["EP-A"]);

        await driver.DisposeAsync();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ExecuteAsync_SameEndpoint_IsSerialized()
    {
        var driver = new FakeMultiEndpointDriver { DelayMs = 30 };
        await driver.InitializeAsync(new PluginInitContext
        {
            PluginApiVersion = PluginApiVersions.V1,
            FrameworkVersion = "test"
        });

        var tasks = new[]
        {
            driver.ExecuteAsync(CreateRequest("EP-X", "1"), CancellationToken.None),
            driver.ExecuteAsync(CreateRequest("EP-X", "2"), CancellationToken.None),
            driver.ExecuteAsync(CreateRequest("EP-X", "3"), CancellationToken.None)
        };

        var results = await Task.WhenAll(tasks);
        Assert.All(results, r => Assert.Equal(StepExecutionStatus.Passed, r.Status));
        Assert.Equal(1, driver.CreateCounts["EP-X"]);
        Assert.Equal(3, driver.SendCounts["EP-X"]);
        Assert.True(driver.MaxConcurrentOnSameEndpoint <= 1);

        await driver.DisposeAsync();
    }

    private static StepExecutionRequest CreateRequest(string endpoint, string command)
    {
        return new StepExecutionRequest
        {
            StepId = Guid.NewGuid().ToString("N"),
            StepName = command,
            StepType = "mock",
            Channel = "mock",
            Command = command,
            TimeoutMs = 5000,
            Parameters = new Dictionary<string, object?>
            {
                ["Endpoint"] = endpoint
            }
        };
    }

    private static async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 2000)
    {
        var start = Environment.TickCount64;
        while (!condition())
        {
            if (Environment.TickCount64 - start > timeoutMs)
            {
                throw new TimeoutException("Condition not met in time.");
            }

            await Task.Delay(10);
        }
    }

    private sealed class FakeMultiEndpointDriver : DeviceDriverPluginBase
    {
        private int _sameEndpointInFlight;

        public ConcurrentDictionary<string, int> CreateCounts { get; } = new(StringComparer.OrdinalIgnoreCase);
        public ConcurrentDictionary<string, int> SendCounts { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Task? BlockSendUntil { get; set; }
        public int DelayMs { get; set; }
        public int MaxConcurrentOnSameEndpoint { get; private set; }
        public int ActiveConnections => ActiveConnectionCount;

        public override PluginMetadata Metadata { get; } = new()
        {
            PluginId = "test.fake.driver",
            Name = "Fake Driver",
            Version = "1.0.0",
            PluginApiVersion = PluginApiVersions.V1,
            SupportedStepTypes = new[] { "mock" },
            SupportedChannels = new[] { "mock" },
            Priority = 1
        };

        public override bool CanHandle(string stepType, string channel) => true;

        protected override Task<object?> CreateConnectionAsync(string endpoint, CancellationToken ct)
        {
            CreateCounts.AddOrUpdate(endpoint, 1, (_, n) => n + 1);
            return Task.FromResult<object?>(new Conn(endpoint));
        }

        protected override async Task<string> SendCommandOnConnectionAsync(object connection, string command, CancellationToken ct)
        {
            var conn = (Conn)connection;
            var inFlight = Interlocked.Increment(ref _sameEndpointInFlight);
            try
            {
                // Track peak concurrency across all endpoints is fine for same-endpoint test;
                // for same endpoint we hold the slot lock so this stays 1.
                MaxConcurrentOnSameEndpoint = Math.Max(MaxConcurrentOnSameEndpoint, inFlight);

                if (BlockSendUntil != null)
                {
                    await BlockSendUntil.WaitAsync(ct).ConfigureAwait(false);
                }

                if (DelayMs > 0)
                {
                    await Task.Delay(DelayMs, ct).ConfigureAwait(false);
                }

                SendCounts.AddOrUpdate(conn.Endpoint, 1, (_, n) => n + 1);
                return $"ok:{conn.Endpoint}:{command}";
            }
            finally
            {
                Interlocked.Decrement(ref _sameEndpointInFlight);
            }
        }

        protected override Task CloseConnectionAsync(object connection, CancellationToken ct)
        {
            return Task.CompletedTask;
        }

        private sealed record Conn(string Endpoint);
    }
}
