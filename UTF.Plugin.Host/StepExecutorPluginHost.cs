using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using UTF.Logging;
using UTF.Plugin.Abstractions;

namespace UTF.Plugin.Host;

public sealed record PluginLoadIssue
{
    public string ManifestPath { get; init; } = string.Empty;
    public string ErrorCode { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
}

public sealed record PluginLoadReport
{
    public int LoadedCount { get; init; }
    public int FailedCount { get; init; }
    public IReadOnlyList<PluginLoadIssue> Issues { get; init; } = Array.Empty<PluginLoadIssue>();
}

public sealed record PluginHealthEntry
{
    public string PluginId { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public bool IsHealthy { get; init; }
    public string Status { get; init; } = string.Empty;
    public IReadOnlyList<string> SupportedStepTypes { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> SupportedChannels { get; init; } = Array.Empty<string>();
}

public sealed record PluginHealthReport
{
    public DateTime CheckTimeUtc { get; init; }
    public int TotalPlugins { get; init; }
    public int HealthyPlugins { get; init; }
    public IReadOnlyList<PluginHealthEntry> Entries { get; init; } = Array.Empty<PluginHealthEntry>();
}

internal sealed record LoadedPlugin
{
    public required PluginLoadContext LoadContext { get; init; }
    public required IStepExecutorPlugin Instance { get; init; }
    public required PluginMetadata Metadata { get; init; }
    public required PluginManifest Manifest { get; init; }
    public required string AssemblyPath { get; init; }
}

public sealed class StepExecutorPluginHost : IDisposable
{
    private readonly string _pluginRoot;
    private readonly ILogger? _logger;
    private readonly List<LoadedPlugin> _loadedPlugins = new();
    private readonly SemaphoreSlim _initSemaphore = new(1, 1);

    private bool _initialized;
    private bool _disposed;

    public StepExecutorPluginHost(string pluginRoot, ILogger? logger = null)
    {
        _pluginRoot = pluginRoot ?? throw new ArgumentNullException(nameof(pluginRoot));
        _logger = logger;
    }

    public IReadOnlyList<PluginMetadata> LoadedPlugins =>
        _loadedPlugins.Select(p => p.Metadata).ToList().AsReadOnly();

    /// <summary>
    /// 按插件 ID 卸载指定插件
    /// </summary>
    public async Task<bool> UnloadPluginAsync(string pluginId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pluginId);

        await _initSemaphore.WaitAsync(cancellationToken);
        try
        {
            var plugin = _loadedPlugins.FirstOrDefault(
                p => string.Equals(p.Metadata.PluginId, pluginId, StringComparison.OrdinalIgnoreCase));

            if (plugin == null)
            {
                _logger?.Warning($"未找到插件: {pluginId}，无法卸载。");
                return false;
            }

            try
            {
                await plugin.Instance.ShutdownAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger?.Warning($"插件关闭异常: {pluginId}, {ex.Message}");
            }

            try
            {
                plugin.LoadContext.Unload();
            }
            catch (Exception ex)
            {
                _logger?.Warning($"插件上下文卸载异常: {pluginId}, {ex.Message}");
            }

            _loadedPlugins.Remove(plugin);
            _logger?.Info($"插件已卸载: {pluginId}");
            return true;
        }
        finally
        {
            _initSemaphore.Release();
        }
    }

    /// <summary>
    /// 重新加载指定插件（卸载旧版本并从原始清单路径重新加载）
    /// </summary>
    public async Task<PluginLoadReport> ReloadPluginAsync(string pluginId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pluginId);

        await _initSemaphore.WaitAsync(cancellationToken);
        try
        {
            var issues = new List<PluginLoadIssue>();
            var existing = _loadedPlugins.FirstOrDefault(
                p => string.Equals(p.Metadata.PluginId, pluginId, StringComparison.OrdinalIgnoreCase));

            string? manifestPath = null;
            if (existing != null)
            {
                manifestPath = Path.Combine(Path.GetDirectoryName(existing.AssemblyPath)!, "plugin.manifest.json");
                await UnloadPluginInternalAsync(existing, cancellationToken);
            }
            else
            {
                // 尝试从插件根目录搜索
                manifestPath = FindManifestByPluginId(pluginId);
            }

            if (manifestPath == null || !File.Exists(manifestPath))
            {
                issues.Add(new PluginLoadIssue
                {
                    ManifestPath = manifestPath ?? "unknown",
                    ErrorCode = PluginErrorCodes.PluginNotFound,
                    Message = $"找不到插件 {pluginId} 的清单文件。"
                });

                return new PluginLoadReport { LoadedCount = 0, FailedCount = 1, Issues = issues };
            }

            try
            {
                var loaded = await LoadPluginAsync(manifestPath, cancellationToken);
                _loadedPlugins.Add(loaded);
                _logger?.Info($"插件重新加载成功: {loaded.Metadata.PluginId} {loaded.Metadata.Version}");
                return new PluginLoadReport { LoadedCount = 1, FailedCount = 0, Issues = issues };
            }
            catch (Exception ex)
            {
                issues.Add(new PluginLoadIssue
                {
                    ManifestPath = manifestPath,
                    ErrorCode = PluginErrorCodes.InitializeFailed,
                    Message = ex.Message
                });
                _logger?.Error($"插件重新加载失败: {pluginId}", ex);
                return new PluginLoadReport { LoadedCount = 0, FailedCount = 1, Issues = issues };
            }
        }
        finally
        {
            _initSemaphore.Release();
        }
    }

    /// <summary>
    /// 版本升级：从新清单路径加载更高版本的插件替换旧版本
    /// </summary>
    public async Task<PluginLoadReport> UpgradePluginAsync(string pluginId, string newManifestPath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pluginId);
        ArgumentNullException.ThrowIfNull(newManifestPath);

        if (!File.Exists(newManifestPath))
        {
            return new PluginLoadReport
            {
                LoadedCount = 0,
                FailedCount = 1,
                Issues = new[]
                {
                    new PluginLoadIssue
                    {
                        ManifestPath = newManifestPath,
                        ErrorCode = PluginErrorCodes.ManifestInvalid,
                        Message = $"新版本清单文件不存在: {newManifestPath}"
                    }
                }
            };
        }

        await _initSemaphore.WaitAsync(cancellationToken);
        try
        {
            var issues = new List<PluginLoadIssue>();

            // 读取新清单并做版本比较
            var newManifest = ReadManifest(newManifestPath);
            if (!string.Equals(newManifest.PluginId, pluginId, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new PluginLoadIssue
                {
                    ManifestPath = newManifestPath,
                    ErrorCode = PluginErrorCodes.UpgradeFailed,
                    Message = $"清单 pluginId '{newManifest.PluginId}' 与目标 '{pluginId}' 不一致。"
                });
                return new PluginLoadReport { LoadedCount = 0, FailedCount = 1, Issues = issues };
            }

            var existing = _loadedPlugins.FirstOrDefault(
                p => string.Equals(p.Metadata.PluginId, pluginId, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                if (CompareVersions(newManifest.Version, existing.Manifest.Version) <= 0)
                {
                    issues.Add(new PluginLoadIssue
                    {
                        ManifestPath = newManifestPath,
                        ErrorCode = PluginErrorCodes.UpgradeFailed,
                        Message = $"新版本 {newManifest.Version} 不高于当前版本 {existing.Manifest.Version}。"
                    });
                    return new PluginLoadReport { LoadedCount = 0, FailedCount = 1, Issues = issues };
                }

                await UnloadPluginInternalAsync(existing, cancellationToken);
            }

            try
            {
                var loaded = await LoadPluginAsync(newManifestPath, cancellationToken);
                _loadedPlugins.Add(loaded);
                _logger?.Info($"插件升级成功: {pluginId} -> {loaded.Metadata.Version}");
                return new PluginLoadReport { LoadedCount = 1, FailedCount = 0, Issues = issues };
            }
            catch (Exception ex)
            {
                issues.Add(new PluginLoadIssue
                {
                    ManifestPath = newManifestPath,
                    ErrorCode = PluginErrorCodes.UpgradeFailed,
                    Message = ex.Message
                });
                _logger?.Error($"插件升级失败: {pluginId}", ex);
                return new PluginLoadReport { LoadedCount = 0, FailedCount = 1, Issues = issues };
            }
        }
        finally
        {
            _initSemaphore.Release();
        }
    }

    /// <summary>
    /// 健康检查：检查所有已加载插件的状态
    /// </summary>
    public PluginHealthReport HealthCheck()
    {
        var entries = new List<PluginHealthEntry>();
        foreach (var plugin in _loadedPlugins)
        {
            try
            {
                var canHandleDefault = plugin.Instance.CanHandle("__healthcheck__", "__healthcheck__");
                entries.Add(new PluginHealthEntry
                {
                    PluginId = plugin.Metadata.PluginId,
                    Version = plugin.Metadata.Version,
                    IsHealthy = true,
                    Status = "正常",
                    SupportedStepTypes = plugin.Metadata.SupportedStepTypes,
                    SupportedChannels = plugin.Metadata.SupportedChannels
                });
            }
            catch (Exception ex)
            {
                entries.Add(new PluginHealthEntry
                {
                    PluginId = plugin.Metadata.PluginId,
                    Version = plugin.Metadata.Version,
                    IsHealthy = false,
                    Status = $"异常: {ex.Message}",
                    SupportedStepTypes = plugin.Metadata.SupportedStepTypes,
                    SupportedChannels = plugin.Metadata.SupportedChannels
                });
            }
        }

        return new PluginHealthReport
        {
            CheckTimeUtc = DateTime.UtcNow,
            TotalPlugins = entries.Count,
            HealthyPlugins = entries.Count(e => e.IsHealthy),
            Entries = entries
        };
    }

    private async Task UnloadPluginInternalAsync(LoadedPlugin plugin, CancellationToken cancellationToken)
    {
        try
        {
            await plugin.Instance.ShutdownAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.Warning($"插件关闭异常: {plugin.Metadata.PluginId}, {ex.Message}");
        }

        try
        {
            plugin.LoadContext.Unload();
        }
        catch (Exception ex)
        {
            _logger?.Warning($"插件上下文卸载异常: {plugin.Metadata.PluginId}, {ex.Message}");
        }

        _loadedPlugins.Remove(plugin);
    }

    private string? FindManifestByPluginId(string pluginId)
    {
        if (!Directory.Exists(_pluginRoot)) return null;

        var manifests = Directory.GetFiles(_pluginRoot, "plugin.manifest.json", SearchOption.AllDirectories);
        foreach (var path in manifests)
        {
            try
            {
                var manifest = ReadManifest(path);
                if (string.Equals(manifest.PluginId, pluginId, StringComparison.OrdinalIgnoreCase))
                {
                    return path;
                }
            }
            catch
            {
                // 跳过无效清单
            }
        }

        return null;
    }

    private static int CompareVersions(string versionA, string versionB)
    {
        if (Version.TryParse(versionA, out var a) && Version.TryParse(versionB, out var b))
        {
            return a.CompareTo(b);
        }

        return string.Compare(versionA, versionB, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<PluginLoadReport> InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _initSemaphore.WaitAsync(cancellationToken);
        try
        {
            if (_initialized)
            {
                return new PluginLoadReport
                {
                    LoadedCount = _loadedPlugins.Count,
                    FailedCount = 0
                };
            }

            var issues = new List<PluginLoadIssue>();
            if (!Directory.Exists(_pluginRoot))
            {
                _logger?.Warning($"插件目录不存在，跳过加载: {_pluginRoot}");
                _initialized = true;
                return new PluginLoadReport
                {
                    LoadedCount = 0,
                    FailedCount = 0
                };
            }

            var manifestPaths = Directory.GetFiles(_pluginRoot, "plugin.manifest.json", SearchOption.AllDirectories);
            foreach (var manifestPath in manifestPaths)
            {
                try
                {
                    var loadedPlugin = await LoadPluginAsync(manifestPath, cancellationToken);
                    _loadedPlugins.Add(loadedPlugin);
                    _logger?.Info($"插件加载成功: {loadedPlugin.Metadata.PluginId} {loadedPlugin.Metadata.Version}");
                }
                catch (PluginLoadException ex)
                {
                    issues.Add(new PluginLoadIssue
                    {
                        ManifestPath = manifestPath,
                        ErrorCode = ex.ErrorCode,
                        Message = ex.Message
                    });
                    _logger?.Warning($"插件加载失败 [{ex.ErrorCode}]: {manifestPath} - {ex.Message}");
                }
                catch (Exception ex)
                {
                    issues.Add(new PluginLoadIssue
                    {
                        ManifestPath = manifestPath,
                        ErrorCode = PluginErrorCodes.InitializeFailed,
                        Message = ex.Message
                    });
                    _logger?.Error($"插件加载异常: {manifestPath}", ex);
                }
            }

            _initialized = true;
            return new PluginLoadReport
            {
                LoadedCount = _loadedPlugins.Count,
                FailedCount = issues.Count,
                Issues = issues
            };
        }
        finally
        {
            _initSemaphore.Release();
        }
    }

    public async Task<StepExecutionResult> ExecuteAsync(StepExecutionRequest request, CancellationToken cancellationToken = default)
    {
        if (!_initialized)
        {
            await InitializeAsync(cancellationToken);
        }

        var matches = _loadedPlugins
            .Where(p => p.Instance.CanHandle(request.StepType, request.Channel))
            .OrderBy(p => p.Metadata.Priority)
            .ThenBy(p => p.Metadata.PluginId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (matches.Count == 0)
        {
            return new StepExecutionResult
            {
                Status = StepExecutionStatus.Error,
                StartTimeUtc = DateTime.UtcNow,
                EndTimeUtc = DateTime.UtcNow,
                ErrorCode = PluginErrorCodes.NoMatchingPlugin,
                ErrorMessage = $"未找到可处理步骤类型 '{request.StepType}' 与通道 '{request.Channel}' 的插件。"
            };
        }

        if (matches.Count > 1 && matches[0].Metadata.Priority == matches[1].Metadata.Priority)
        {
            var candidates = string.Join(", ", matches.Select(m => m.Metadata.PluginId));
            return new StepExecutionResult
            {
                Status = StepExecutionStatus.Error,
                StartTimeUtc = DateTime.UtcNow,
                EndTimeUtc = DateTime.UtcNow,
                ErrorCode = PluginErrorCodes.MultipleMatchingPlugins,
                ErrorMessage = $"存在多个同优先级插件匹配: {candidates}"
            };
        }

        var selected = matches[0];
        var startedAt = DateTime.UtcNow;

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var timeoutMs = request.TimeoutMs > 0 ? request.TimeoutMs : 30000;
        timeoutCts.CancelAfter(timeoutMs);

        try
        {
            var result = await selected.Instance.ExecuteAsync(request, timeoutCts.Token);
            return NormalizeResult(result, selected.Metadata, startedAt);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new StepExecutionResult
            {
                Status = StepExecutionStatus.Timeout,
                StartTimeUtc = startedAt,
                EndTimeUtc = DateTime.UtcNow,
                ErrorCode = PluginErrorCodes.ExecuteTimeout,
                ErrorMessage = $"插件执行超时: {selected.Metadata.PluginId}",
                PluginId = selected.Metadata.PluginId,
                PluginVersion = selected.Metadata.Version
            };
        }
        catch (Exception ex)
        {
            return new StepExecutionResult
            {
                Status = StepExecutionStatus.Error,
                StartTimeUtc = startedAt,
                EndTimeUtc = DateTime.UtcNow,
                ErrorCode = PluginErrorCodes.ExecuteException,
                ErrorMessage = ex.Message,
                PluginId = selected.Metadata.PluginId,
                PluginVersion = selected.Metadata.Version
            };
        }
    }

    private async Task<LoadedPlugin> LoadPluginAsync(string manifestPath, CancellationToken cancellationToken)
    {
        var manifest = ReadManifest(manifestPath);
        ValidateManifest(manifest, manifestPath);

        if (!string.Equals(manifest.PluginApiVersion, PluginApiVersions.V1, StringComparison.OrdinalIgnoreCase))
        {
            throw new PluginLoadException(
                PluginErrorCodes.ApiVersionIncompatible,
                $"插件 API 版本不兼容: {manifest.PluginApiVersion}");
        }

        var manifestDirectory = Path.GetDirectoryName(manifestPath)
            ?? throw new PluginLoadException(PluginErrorCodes.ManifestInvalid, "插件清单路径无效。");
        var assemblyPath = Path.GetFullPath(Path.Combine(manifestDirectory, manifest.EntryAssembly));
        if (!File.Exists(assemblyPath))
        {
            throw new PluginLoadException(
                PluginErrorCodes.ManifestInvalid,
                $"入口程序集不存在: {assemblyPath}");
        }

        if (!string.IsNullOrWhiteSpace(manifest.Sha256))
        {
            VerifySha256OrThrow(assemblyPath, manifest.Sha256);
        }

        var loadContext = new PluginLoadContext(assemblyPath);
        var assembly = loadContext.LoadFromAssemblyPath(assemblyPath);
        var entryType = assembly.GetType(manifest.EntryType, throwOnError: true, ignoreCase: false)
            ?? throw new PluginLoadException(PluginErrorCodes.ManifestInvalid, $"入口类型不存在: {manifest.EntryType}");

        if (!typeof(IStepExecutorPlugin).IsAssignableFrom(entryType))
        {
            throw new PluginLoadException(
                PluginErrorCodes.ManifestInvalid,
                $"入口类型未实现 IStepExecutorPlugin: {manifest.EntryType}");
        }

        var pluginInstance = Activator.CreateInstance(entryType) as IStepExecutorPlugin
            ?? throw new PluginLoadException(PluginErrorCodes.InitializeFailed, "插件实例化失败。");

        try
        {
            var initContext = new PluginInitContext
            {
                FrameworkVersion = manifest.FrameworkVersion ?? string.Empty,
                PluginApiVersion = manifest.PluginApiVersion,
                PluginDirectory = manifestDirectory,
                Settings = manifest.Settings
            };

            await pluginInstance.InitializeAsync(initContext, cancellationToken);
            var metadata = MergeMetadata(pluginInstance.Metadata, manifest);

            return new LoadedPlugin
            {
                LoadContext = loadContext,
                Instance = pluginInstance,
                Metadata = metadata,
                Manifest = manifest,
                AssemblyPath = assemblyPath
            };
        }
        catch (Exception ex)
        {
            try
            {
                loadContext.Unload();
            }
            catch
            {
                // ignore unload failure
            }

            throw new PluginLoadException(PluginErrorCodes.InitializeFailed, ex.Message);
        }
    }

    private static StepExecutionResult NormalizeResult(StepExecutionResult result, PluginMetadata metadata, DateTime startedAt)
    {
        var endTime = result.EndTimeUtc == default ? DateTime.UtcNow : result.EndTimeUtc;
        return result with
        {
            StartTimeUtc = result.StartTimeUtc == default ? startedAt : result.StartTimeUtc,
            EndTimeUtc = endTime,
            PluginId = string.IsNullOrWhiteSpace(result.PluginId) ? metadata.PluginId : result.PluginId,
            PluginVersion = string.IsNullOrWhiteSpace(result.PluginVersion) ? metadata.Version : result.PluginVersion
        };
    }

    private static PluginMetadata MergeMetadata(PluginMetadata metadata, PluginManifest manifest)
    {
        return metadata with
        {
            PluginId = string.IsNullOrWhiteSpace(metadata.PluginId) ? manifest.PluginId : metadata.PluginId,
            Name = string.IsNullOrWhiteSpace(metadata.Name) ? manifest.Name : metadata.Name,
            Version = string.IsNullOrWhiteSpace(metadata.Version) ? manifest.Version : metadata.Version,
            PluginApiVersion = string.IsNullOrWhiteSpace(metadata.PluginApiVersion)
                ? manifest.PluginApiVersion
                : metadata.PluginApiVersion,
            SupportedStepTypes = metadata.SupportedStepTypes.Count > 0
                ? metadata.SupportedStepTypes
                : manifest.SupportedStepTypes,
            SupportedChannels = metadata.SupportedChannels.Count > 0
                ? metadata.SupportedChannels
                : manifest.SupportedChannels,
            Priority = manifest.Priority
        };
    }

    private static PluginManifest ReadManifest(string manifestPath)
    {
        try
        {
            var json = File.ReadAllText(manifestPath, Encoding.UTF8);
            return JsonSerializer.Deserialize<PluginManifest>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new PluginLoadException(PluginErrorCodes.ManifestInvalid, "插件清单解析为空。");
        }
        catch (JsonException ex)
        {
            throw new PluginLoadException(PluginErrorCodes.ManifestInvalid, $"插件清单 JSON 无效: {ex.Message}");
        }
    }

    private static void ValidateManifest(PluginManifest manifest, string manifestPath)
    {
        if (string.IsNullOrWhiteSpace(manifest.PluginId) ||
            string.IsNullOrWhiteSpace(manifest.EntryAssembly) ||
            string.IsNullOrWhiteSpace(manifest.EntryType) ||
            string.IsNullOrWhiteSpace(manifest.PluginApiVersion))
        {
            throw new PluginLoadException(
                PluginErrorCodes.ManifestInvalid,
                $"插件清单缺少必填字段: {manifestPath}");
        }
    }

    private static void VerifySha256OrThrow(string assemblyPath, string expectedSha256)
    {
        var normalizedExpected = expectedSha256.Trim().ToLowerInvariant();
        using var stream = File.OpenRead(assemblyPath);
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(stream);
        var actual = Convert.ToHexString(hash).ToLowerInvariant();
        if (!string.Equals(actual, normalizedExpected, StringComparison.OrdinalIgnoreCase))
        {
            throw new PluginLoadException(
                PluginErrorCodes.IntegrityCheckFailed,
                $"插件哈希校验失败: {Path.GetFileName(assemblyPath)}");
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        foreach (var plugin in _loadedPlugins)
        {
            try
            {
                plugin.Instance.ShutdownAsync(CancellationToken.None).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _logger?.Warning($"插件关闭异常: {plugin.Metadata.PluginId}, {ex.Message}");
            }

            try
            {
                plugin.LoadContext.Unload();
            }
            catch (Exception ex)
            {
                _logger?.Warning($"插件卸载异常: {plugin.Metadata.PluginId}, {ex.Message}");
            }
        }

        _loadedPlugins.Clear();
        _initSemaphore.Dispose();
        _disposed = true;
    }
}

internal sealed class PluginLoadException : Exception
{
    public PluginLoadException(string errorCode, string message) : base(message)
    {
        ErrorCode = errorCode;
    }

    public string ErrorCode { get; }
}
