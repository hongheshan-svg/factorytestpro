using System.Text.Json;
using UTF.Plugin.Abstractions;

namespace UTF.Core.Tests;

internal static class StepExecutorPluginHostTestAssets
{
    public static string CreateTempDirectory(List<string> tempDirectories)
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        tempDirectories.Add(directory);
        return directory;
    }

    public static string CreateNonExistentPath()
    {
        return Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    }

    public static void CopyRealCmdPluginPackage(string pluginRoot, string pluginDirectoryName)
    {
        var sourceManifestPath = FindRepositoryFile(Path.Combine("plugins", "utf.executor.cmd", "1.0.0", "plugin.manifest.json"));
        var sourceAssemblyPath = typeof(UTF.Plugins.Example.CmdStepExecutorPlugin).Assembly.Location;
        var sourceDepsPath = Path.ChangeExtension(sourceAssemblyPath, ".deps.json");

        var packageDirectory = Path.Combine(pluginRoot, pluginDirectoryName, "1.0.0");
        Directory.CreateDirectory(packageDirectory);

        File.Copy(sourceManifestPath, Path.Combine(packageDirectory, "plugin.manifest.json"), overwrite: true);
        File.Copy(sourceAssemblyPath, Path.Combine(packageDirectory, Path.GetFileName(sourceAssemblyPath)), overwrite: true);

        if (File.Exists(sourceDepsPath))
        {
            File.Copy(sourceDepsPath, Path.Combine(packageDirectory, Path.GetFileName(sourceDepsPath)), overwrite: true);
        }
    }

    public static void CopyTestPluginPackage(
        string pluginRoot,
        string pluginDirectoryName,
        string entryType,
        int priority,
        string pluginApiVersion = PluginApiVersions.V1,
        string? sha256 = null)
    {
        var sourceOutputDirectory = Path.GetDirectoryName(typeof(StepExecutorPluginHostTests).Assembly.Location)
            ?? throw new DirectoryNotFoundException("Could not locate the test output directory.");
        var packageDirectory = Path.Combine(pluginRoot, pluginDirectoryName, "1.0.0");
        Directory.CreateDirectory(packageDirectory);

        foreach (var sourcePath in Directory.GetFiles(sourceOutputDirectory))
        {
            var extension = Path.GetExtension(sourcePath);
            if (!string.Equals(extension, ".dll", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(extension, ".json", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            File.Copy(sourcePath, Path.Combine(packageDirectory, Path.GetFileName(sourcePath)), overwrite: true);
        }

        WriteManifest(
            packageDirectory,
            new
            {
                pluginId = pluginDirectoryName,
                name = pluginDirectoryName,
                version = "1.0.0",
                pluginApiVersion,
                entryAssembly = "UTF.Core.Tests.dll",
                entryType,
                supportedStepTypes = new[] { "custom" },
                supportedChannels = new[] { "cmd" },
                priority,
                frameworkVersion = "net10.0",
                sha256
            });
    }

    public static void CreateInvalidJsonManifestPackage(string pluginRoot, string pluginDirectoryName)
    {
        var packageDirectory = Path.Combine(pluginRoot, pluginDirectoryName, "1.0.0");
        Directory.CreateDirectory(packageDirectory);
        File.WriteAllText(Path.Combine(packageDirectory, "plugin.manifest.json"), "{ invalid json");
    }

    private static void WriteManifest(string packageDirectory, object manifest)
    {
        var manifestPath = Path.Combine(packageDirectory, "plugin.manifest.json");
        var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });
        File.WriteAllText(manifestPath, json);
    }

    private static string FindRepositoryFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            var solutionPath = Path.Combine(directory.FullName, "UniversalTestFramework.sln");
            if (File.Exists(solutionPath))
            {
                return Path.Combine(directory.FullName, relativePath);
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root from the test output directory.");
    }
}

public sealed class LowPriorityPassingPlugin : IStepExecutorPlugin
{
    public PluginMetadata Metadata => new()
    {
        PluginId = "priority.low",
        Name = "Priority Low",
        Version = "1.0.0",
        PluginApiVersion = PluginApiVersions.V1,
        SupportedStepTypes = new[] { "custom" },
        SupportedChannels = new[] { "cmd" },
        Priority = 10
    };

    public Task InitializeAsync(PluginInitContext context, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public bool CanHandle(string stepType, string channel)
    {
        return string.Equals(stepType, "custom", StringComparison.OrdinalIgnoreCase) &&
               string.Equals(channel, "cmd", StringComparison.OrdinalIgnoreCase);
    }

    public Task<StepExecutionResult> ExecuteAsync(StepExecutionRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new StepExecutionResult
        {
            Status = StepExecutionStatus.Passed,
            RawOutput = "selected-low-priority",
            NormalizedOutput = "selected-low-priority",
            PluginId = Metadata.PluginId,
            PluginVersion = Metadata.Version
        });
    }

    public Task ShutdownAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

public sealed class HighPriorityFailingPlugin : IStepExecutorPlugin
{
    public PluginMetadata Metadata => new()
    {
        PluginId = "priority.high",
        Name = "Priority High",
        Version = "1.0.0",
        PluginApiVersion = PluginApiVersions.V1,
        SupportedStepTypes = new[] { "custom" },
        SupportedChannels = new[] { "cmd" },
        Priority = 200
    };

    public Task InitializeAsync(PluginInitContext context, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public bool CanHandle(string stepType, string channel)
    {
        return string.Equals(stepType, "custom", StringComparison.OrdinalIgnoreCase) &&
               string.Equals(channel, "cmd", StringComparison.OrdinalIgnoreCase);
    }

    public Task<StepExecutionResult> ExecuteAsync(StepExecutionRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new StepExecutionResult
        {
            Status = StepExecutionStatus.Failed,
            RawOutput = "selected-high-priority",
            NormalizedOutput = "selected-high-priority",
            PluginId = Metadata.PluginId,
            PluginVersion = Metadata.Version,
            ErrorCode = "TEST_HIGH_PRIORITY_SHOULD_NOT_RUN",
            ErrorMessage = "This plugin should not be selected when a lower numeric priority exists."
        });
    }

    public Task ShutdownAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}