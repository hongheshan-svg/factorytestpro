using System.Collections.ObjectModel;

namespace UTF.Plugin.Abstractions;

public static class PluginApiVersions
{
    public const string V1 = "1.0";
}

public static class PluginErrorCodes
{
    public const string ManifestInvalid = "PLG001";
    public const string IntegrityCheckFailed = "PLG002";
    public const string ApiVersionIncompatible = "PLG003";
    public const string InitializeFailed = "PLG004";
    public const string NoMatchingPlugin = "PLG005";
    public const string MultipleMatchingPlugins = "PLG006";
    public const string ExecuteTimeout = "PLG007";
    public const string ExecuteException = "PLG008";
    public const string PluginNotFound = "PLG009";
    public const string UpgradeFailed = "PLG010";
}

public enum StepExecutionStatus
{
    Passed = 0,
    Failed = 1,
    Timeout = 2,
    Error = 3,
    Skipped = 4
}

public sealed record PluginMetadata
{
    public string PluginId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Version { get; init; } = "1.0.0";
    public string PluginApiVersion { get; init; } = PluginApiVersions.V1;
    public IReadOnlyList<string> SupportedStepTypes { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> SupportedChannels { get; init; } = Array.Empty<string>();
    public int Priority { get; init; } = 100;
}

public sealed record PluginArtifact
{
    public string Name { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
}

public sealed record PluginInitContext
{
    public string FrameworkVersion { get; init; } = string.Empty;
    public string PluginApiVersion { get; init; } = PluginApiVersions.V1;
    public string PluginDirectory { get; init; } = string.Empty;
    public IReadOnlyDictionary<string, string> Settings { get; init; } =
        new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());
}

public sealed record StepExecutionRequest
{
    public string StepId { get; init; } = string.Empty;
    public string StepName { get; init; } = string.Empty;
    public string StepType { get; init; } = string.Empty;
    public string Channel { get; init; } = string.Empty;
    public string Command { get; init; } = string.Empty;
    public int TimeoutMs { get; init; } = 30000;
    public string DutId { get; init; } = string.Empty;
    public string SessionId { get; init; } = string.Empty;
    public string StationId { get; init; } = string.Empty;
    public IReadOnlyDictionary<string, object?> Parameters { get; init; } =
        new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?>());
}

public sealed record StepExecutionResult
{
    public StepExecutionStatus Status { get; init; } = StepExecutionStatus.Error;
    public string RawOutput { get; init; } = string.Empty;
    public string NormalizedOutput { get; init; } = string.Empty;
    public DateTime StartTimeUtc { get; init; } = DateTime.UtcNow;
    public DateTime EndTimeUtc { get; init; } = DateTime.UtcNow;
    public string ErrorCode { get; init; } = string.Empty;
    public string ErrorMessage { get; init; } = string.Empty;
    public string PluginId { get; init; } = string.Empty;
    public string PluginVersion { get; init; } = string.Empty;
    public IReadOnlyDictionary<string, object?> ExtendedData { get; init; } =
        new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?>());
    public IReadOnlyList<PluginArtifact> Artifacts { get; init; } = Array.Empty<PluginArtifact>();
}

public interface IStepExecutorPlugin
{
    PluginMetadata Metadata { get; }

    Task InitializeAsync(PluginInitContext context, CancellationToken cancellationToken = default);

    bool CanHandle(string stepType, string channel);

    Task<StepExecutionResult> ExecuteAsync(StepExecutionRequest request, CancellationToken cancellationToken = default);

    Task ShutdownAsync(CancellationToken cancellationToken = default);
}
