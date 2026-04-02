using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UTF.Core;
using UTF.Core.Events;
using UTF.Logging;
using Xunit;

namespace UTF.Core.Tests;

public class EventBusIntegrationTests : IDisposable
{
    private readonly ILogger _logger = LoggerFactory.CreateLogger(
        "Test",
        new LogConfiguration
        {
            EnableConsole = false,
            EnableFile = false
        });

    public void Dispose()
    {
        if (_logger is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    [Fact]
    public async Task PublishAsync_WithSubscriber_ReceivesEvent()
    {
        var bus = new EventBus(_logger);
        TestCompletedEvent? received = null;

        bus.Subscribe<TestCompletedEvent>(evt =>
        {
            received = evt;
            return Task.CompletedTask;
        });

        await bus.PublishAsync(new TestCompletedEvent("DUT1", "T1", true, DateTime.UtcNow));

        Assert.NotNull(received);
        Assert.Equal("DUT1", received!.DutId);
        Assert.True(received.Passed);
    }

    [Fact]
    public async Task PublishAsync_NoSubscribers_DoesNotThrow()
    {
        var bus = new EventBus(_logger);

        await bus.PublishAsync(new TestStartedEvent("DUT1", "T1", DateTime.UtcNow));
    }

    [Fact]
    public async Task Subscribe_Dispose_Unsubscribes()
    {
        var bus = new EventBus(_logger);
        int callCount = 0;

        var sub = bus.Subscribe<TestStartedEvent>(_ =>
        {
            callCount++;
            return Task.CompletedTask;
        });

        await bus.PublishAsync(new TestStartedEvent("DUT1", "T1", DateTime.UtcNow));
        Assert.Equal(1, callCount);

        sub.Dispose();

        await bus.PublishAsync(new TestStartedEvent("DUT1", "T2", DateTime.UtcNow));
        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task PublishAsync_MultipleSubscribers_AllReceive()
    {
        var bus = new EventBus(_logger);
        var received = new List<string>();

        bus.Subscribe<TestStepCompletedEvent>(evt =>
        {
            received.Add("sub1:" + evt.StepId);
            return Task.CompletedTask;
        });

        bus.Subscribe<TestStepCompletedEvent>(evt =>
        {
            received.Add("sub2:" + evt.StepId);
            return Task.CompletedTask;
        });

        await bus.PublishAsync(new TestStepCompletedEvent("DUT1", "S1", true, DateTime.UtcNow));

        Assert.Equal(2, received.Count);
        Assert.Contains("sub1:S1", received);
        Assert.Contains("sub2:S1", received);
    }

    [Fact]
    public async Task Engine_PublishesEvents_WhenEventBusProvided()
    {
        var bus = new EventBus(_logger);
        var events = new List<IEvent>();

        bus.Subscribe<TestStartedEvent>(evt => { events.Add(evt); return Task.CompletedTask; });
        bus.Subscribe<TestStepCompletedEvent>(evt => { events.Add(evt); return Task.CompletedTask; });
        bus.Subscribe<TestCompletedEvent>(evt => { events.Add(evt); return Task.CompletedTask; });

        var engine = new ConfigDrivenTestEngine(_logger, eventBus: bus);
        var project = new ConfigTestProject
        {
            Id = "P1",
            Name = "EventTest",
            Enabled = true,
            Steps = new List<ConfigTestStep>
            {
                new() { Id = "S1", Name = "Step1", Order = 1, Type = "serial", Command = "echo ok", Expected = "contains:ok" }
            }
        };

        await engine.ExecuteTestProjectAsync(project, "DUT_001");

        Assert.Contains(events, e => e is TestStartedEvent);
        Assert.Contains(events, e => e is TestStepCompletedEvent);
        Assert.Contains(events, e => e is TestCompletedEvent);
    }

    [Fact]
    public async Task Engine_DoesNotThrow_WhenEventBusNull()
    {
        var engine = new ConfigDrivenTestEngine(_logger);
        var project = new ConfigTestProject
        {
            Id = "P1",
            Name = "NoEventBus",
            Enabled = true,
            Steps = new List<ConfigTestStep>
            {
                new() { Id = "S1", Name = "Step1", Order = 1, Enabled = false }
            }
        };

        var report = await engine.ExecuteTestProjectAsync(project, "DUT_001");

        Assert.NotNull(report);
    }
}
