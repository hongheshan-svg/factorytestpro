using System;
using System.Collections.Generic;
using System.Linq;

namespace UTF.Core.Mapping;

/// <summary>
/// Pure mapping helpers that convert configuration-shaped data into engine models.
/// UI and configuration loaders should call these instead of reimplementing step walks.
/// </summary>
public static class TestProjectMapper
{
    /// <summary>
    /// Builds a <see cref="ConfigTestProject"/> from a ready list of steps (already mapped).
    /// Applies default retry when a step omits <see cref="ConfigTestStep.RetryCount"/>.
    /// </summary>
    public static ConfigTestProject BuildProject(
        string id,
        string name,
        string? description,
        bool enabled,
        IEnumerable<ConfigTestStep> steps,
        int defaultRetryCount = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(steps);

        return new ConfigTestProject
        {
            Id = id,
            Name = string.IsNullOrWhiteSpace(name) ? id : name,
            Description = description ?? string.Empty,
            Enabled = enabled,
            Steps = steps.Select(step => NormalizeStep(step, defaultRetryCount)).ToList()
        };
    }

    /// <summary>
    /// Clones and normalizes an existing project (default retry, defensive copies of dictionaries).
    /// </summary>
    public static ConfigTestProject CloneProject(ConfigTestProject source, int defaultRetryCount = 0)
    {
        ArgumentNullException.ThrowIfNull(source);
        return BuildProject(
            source.Id,
            source.Name,
            source.Description,
            source.Enabled,
            source.Steps ?? Enumerable.Empty<ConfigTestStep>(),
            defaultRetryCount);
    }

    /// <summary>
    /// Builds <see cref="DUTConfigInfo"/> used by the orchestrator for concurrency and endpoints.
    /// </summary>
    public static DUTConfigInfo BuildDutConfig(
        string? productName = null,
        string? productModel = null,
        string? expectedSoftwareVersion = null,
        int defaultMaxConcurrent = 16,
        int testTimeout = 300,
        int retryCount = 3,
        IEnumerable<string>? serialPorts = null,
        IEnumerable<string>? networkHosts = null)
    {
        return new DUTConfigInfo
        {
            ProductName = productName ?? string.Empty,
            ProductModel = productModel ?? string.Empty,
            ExpectedSoftwareVersion = expectedSoftwareVersion ?? string.Empty,
            DefaultMaxConcurrent = Math.Clamp(defaultMaxConcurrent, 1, 256),
            TestTimeout = Math.Max(1, testTimeout),
            RetryCount = Math.Clamp(retryCount, 0, 10),
            SerialPorts = serialPorts?.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.Trim()).ToList()
                ?? new List<string>(),
            NetworkHosts = networkHosts?.Where(h => !string.IsNullOrWhiteSpace(h)).Select(h => h.Trim()).ToList()
                ?? new List<string>()
        };
    }

    /// <summary>
    /// Builds per-DUT execution context (SerialPort / Host / identity keys) from endpoint lists.
    /// Index is zero-based among the provided DUT id list.
    /// </summary>
    public static Dictionary<string, object> BuildDutContext(
        string dutId,
        string? dutName,
        string? dutType,
        string? serialPort,
        string? host)
    {
        return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["SerialPort"] = serialPort ?? string.Empty,
            ["Host"] = host ?? string.Empty,
            ["DutId"] = dutId,
            ["DutName"] = dutName ?? dutId,
            ["DutType"] = dutType ?? string.Empty
        };
    }

    private static ConfigTestStep NormalizeStep(ConfigTestStep step, int defaultRetryCount)
    {
        return new ConfigTestStep
        {
            Id = string.IsNullOrWhiteSpace(step.Id) ? Guid.NewGuid().ToString("N") : step.Id,
            Name = step.Name ?? string.Empty,
            Description = step.Description ?? string.Empty,
            Order = step.Order,
            Enabled = step.Enabled,
            Type = step.Type,
            TargetDeviceId = step.TargetDeviceId,
            Command = step.Command,
            Expected = step.Expected,
            Timeout = step.Timeout,
            Delay = step.Delay,
            RetryCount = step.RetryCount ?? defaultRetryCount,
            Channel = step.Channel,
            StoreResultAs = step.StoreResultAs,
            ConditionExpression = step.ConditionExpression,
            ContinueOnFailure = step.ContinueOnFailure,
            ValidationRules = step.ValidationRules == null
                ? null
                : new Dictionary<string, object>(step.ValidationRules),
            Parameters = step.Parameters == null
                ? null
                : new Dictionary<string, object>(step.Parameters)
        };
    }
}
