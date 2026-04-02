using System;

namespace UTF.Core.Events;

public record TestStartedEvent(string DutId, string TestId, DateTime Timestamp) : IEvent;

public record TestCompletedEvent(string DutId, string TestId, bool Passed, DateTime Timestamp) : IEvent;

public record TestStepCompletedEvent(string DutId, string StepId, bool Passed, DateTime Timestamp) : IEvent;

public record DeviceStatusChangedEvent(string DeviceId, string Status, DateTime Timestamp) : IEvent;

public record ConfigurationChangedEvent(string ConfigPath, string User, DateTime Timestamp) : IEvent;
