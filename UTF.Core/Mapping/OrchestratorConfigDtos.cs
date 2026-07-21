using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace UTF.Core.Mapping;

/// <summary>
/// Typed DTO for <c>TestProjectConfiguration</c> section deserialization (avoids dynamic).
/// Shape matches unified-config.json / UI UnifiedConfiguration models.
/// </summary>
public sealed class OrchestratorTestProjectSection
{
    public OrchestratorTestProjectData? TestProject { get; set; }
}

/// <summary>
/// Project node under TestProjectConfiguration.
/// </summary>
public sealed class OrchestratorTestProjectData
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public bool? Enabled { get; set; }
    public List<OrchestratorStepData>? Steps { get; set; }
}

/// <summary>
/// Step node used when mapping config sections into <see cref="ConfigTestStep"/>.
/// </summary>
public sealed class OrchestratorStepData
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public int? Order { get; set; }
    public bool? Enabled { get; set; }
    public string? Type { get; set; }
    public string? Target { get; set; }
    public string? TargetDeviceId { get; set; }
    public string? Command { get; set; }
    public string? Expected { get; set; }
    public int? Timeout { get; set; }
    public int? Delay { get; set; }
    public int? RetryCount { get; set; }
    public string? Channel { get; set; }
    public string? StoreResultAs { get; set; }
    public string? ConditionExpression { get; set; }
    public bool? ContinueOnFailure { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }

    public Dictionary<string, object>? ValidationRules { get; set; }
    public Dictionary<string, object>? Parameters { get; set; }
}

/// <summary>
/// Typed DTO for <c>DUTConfiguration</c> section deserialization (avoids dynamic).
/// </summary>
public sealed class OrchestratorDutSection
{
    public OrchestratorProductInfo? ProductInfo { get; set; }
    public OrchestratorGlobalSettings? GlobalSettings { get; set; }
    public OrchestratorCommunicationEndpoints? CommunicationEndpoints { get; set; }
}

public sealed class OrchestratorProductInfo
{
    public string? Name { get; set; }
    public string? Model { get; set; }
    public string? ExpectedSoftwareVersion { get; set; }
}

public sealed class OrchestratorGlobalSettings
{
    public int? DefaultMaxConcurrent { get; set; }
    public int? TestTimeout { get; set; }
    public int? RetryCount { get; set; }
}

public sealed class OrchestratorCommunicationEndpoints
{
    public List<string>? SerialPorts { get; set; }
    public List<string>? NetworkHosts { get; set; }
}
