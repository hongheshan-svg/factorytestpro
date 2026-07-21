using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

namespace UTF.Plugin.Abstractions;

/// <summary>
/// 设备驱动插件基类 - 提供按端点隔离的连接池、超时控制和结果封装。
/// 多 DUT 并行时：同一 endpoint 串行，不同 endpoint 可并发，互不重连抢占。
/// 子类实现连接句柄的创建/发送/关闭即可。
/// </summary>
public abstract class DeviceDriverPluginBase : IStepExecutorPlugin, IDeviceDriverPlugin, IDisposable, IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, EndpointSlot> _slots =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly object _primaryLock = new();
    private string _primaryEndpoint = string.Empty;
    private bool _disposed;

    /// <summary>
    /// 是否至少有一个端点已连接。
    /// </summary>
    protected bool IsConnected => _slots.Values.Any(s => s.Connection != null);

    /// <summary>
    /// 最近一次 <see cref="ConnectAsync"/> 的端点（兼容单连接 API）。
    /// </summary>
    protected string CurrentEndpoint
    {
        get
        {
            lock (_primaryLock)
            {
                return _primaryEndpoint;
            }
        }
    }

    /// <summary>
    /// 当前活跃连接数（测试/诊断用）。
    /// </summary>
    protected int ActiveConnectionCount => _slots.Count(kv => kv.Value.Connection != null);

    public abstract PluginMetadata Metadata { get; }

    public virtual Task InitializeAsync(PluginInitContext context, CancellationToken ct = default)
    {
        if (!string.Equals(context.PluginApiVersion, PluginApiVersions.V1, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"不支持的插件 API 版本: {context.PluginApiVersion}，当前插件仅支持 {PluginApiVersions.V1}");
        }

        OnInitialize(context);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 子类可重写以处理初始化设置（如从 Settings 加载波特率等参数）
    /// </summary>
    protected virtual void OnInitialize(PluginInitContext context) { }

    /// <summary>
    /// 判断本插件能否处理指定的步骤类型与通道。
    /// 采用 <b>AND 语义</b>：<paramref name="stepType"/> 与 <paramref name="channel"/>
    /// 必须同时匹配各自支持的集合；任一集合包含通配符 <c>"*"</c> 时该侧视为恒匹配。
    /// </summary>
    public abstract bool CanHandle(string stepType, string channel);

    /// <summary>
    /// 统一的 AND 语义匹配辅助方法。
    /// </summary>
    protected static bool DefaultCanHandle(
        string stepType,
        string channel,
        IReadOnlySet<string> supportedTypes,
        IReadOnlySet<string> supportedChannels)
    {
        var typeMatch = supportedTypes.Contains("*") ||
                        supportedTypes.Contains(stepType ?? string.Empty, StringComparer.OrdinalIgnoreCase);
        var channelMatch = supportedChannels.Contains("*") ||
                           supportedChannels.Contains(channel ?? string.Empty, StringComparer.OrdinalIgnoreCase);
        return typeMatch && channelMatch;
    }

    public async Task<StepExecutionResult> ExecuteAsync(StepExecutionRequest request, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(request);

        var startedAt = DateTime.UtcNow;
        try
        {
            if (string.IsNullOrWhiteSpace(request.Command))
            {
                return BuildResult(StepExecutionStatus.Failed, startedAt,
                    errorCode: "PLG_DRV_001", errorMessage: "命令为空，无法执行。");
            }

            var endpoint = ResolveEndpoint(request);
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                // 无端点时仍尝试在“空端点”槽上执行（部分 mock/自定义驱动）
                endpoint = string.Empty;
            }

            var slot = GetOrCreateSlot(endpoint);
            await slot.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (slot.Connection == null)
                {
                    var connection = await CreateConnectionAsync(endpoint, cancellationToken).ConfigureAwait(false);
                    if (connection == null)
                    {
                        return BuildResult(StepExecutionStatus.Failed, startedAt,
                            errorCode: "PLG_DRV_002",
                            errorMessage: $"连接端点失败: {endpoint}");
                    }

                    slot.Connection = connection;
                    SetPrimaryEndpoint(endpoint);
                }

                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(request.TimeoutMs);

                var output = await SendCommandOnConnectionAsync(slot.Connection, request.Command, timeoutCts.Token)
                    .ConfigureAwait(false);

                output = PostProcessOutput(output, request);

                var expected = TryGetExpectedExpression(request.Parameters);
                if (!string.IsNullOrWhiteSpace(expected) &&
                    !ExpectedResultMatcher.Match(expected, output, out var reason))
                {
                    return BuildResult(StepExecutionStatus.Failed, startedAt, output,
                        "PLG_DRV_003", reason);
                }

                return BuildResult(StepExecutionStatus.Passed, startedAt, rawOutput: output);
            }
            finally
            {
                slot.Gate.Release();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return BuildResult(StepExecutionStatus.Timeout, startedAt,
                errorCode: PluginErrorCodes.ExecuteTimeout,
                errorMessage: $"命令执行超时 ({request.TimeoutMs}ms)。");
        }
        catch (Exception ex)
        {
            return BuildResult(StepExecutionStatus.Error, startedAt,
                errorCode: PluginErrorCodes.ExecuteException,
                errorMessage: ex.Message);
        }
    }

    /// <summary>
    /// 连接指定端点（按端点隔离；不会断开其他端点）。
    /// </summary>
    public async Task<bool> ConnectAsync(string endpoint, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        endpoint ??= string.Empty;

        var slot = GetOrCreateSlot(endpoint);
        await slot.Gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (slot.Connection != null)
            {
                SetPrimaryEndpoint(endpoint);
                return true;
            }

            var connection = await CreateConnectionAsync(endpoint, ct).ConfigureAwait(false);
            if (connection == null)
            {
                return false;
            }

            slot.Connection = connection;
            SetPrimaryEndpoint(endpoint);
            return true;
        }
        finally
        {
            slot.Gate.Release();
        }
    }

    /// <summary>
    /// 在主端点上发送命令（兼容 <see cref="IDeviceDriverPlugin"/>）。
    /// </summary>
    public async Task<string> SendCommandAsync(string command, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var endpoint = CurrentEndpoint;
        var slot = GetOrCreateSlot(endpoint);
        await slot.Gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (slot.Connection == null)
            {
                throw new InvalidOperationException("尚未连接任何端点。");
            }

            return await SendCommandOnConnectionAsync(slot.Connection, command, ct).ConfigureAwait(false);
        }
        finally
        {
            slot.Gate.Release();
        }
    }

    /// <summary>
    /// 断开主端点；若主端点为空则断开全部。
    /// </summary>
    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        var primary = CurrentEndpoint;
        if (!string.IsNullOrEmpty(primary) && _slots.ContainsKey(primary))
        {
            await DisconnectEndpointAsync(primary, ct).ConfigureAwait(false);
            return;
        }

        await DisconnectAllAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// 断开指定端点连接。
    /// </summary>
    protected async Task DisconnectEndpointAsync(string endpoint, CancellationToken ct = default)
    {
        if (!_slots.TryGetValue(endpoint, out var slot))
        {
            return;
        }

        await slot.Gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (slot.Connection != null)
            {
                try
                {
                    await CloseConnectionAsync(slot.Connection, ct).ConfigureAwait(false);
                }
                catch
                {
                    // 断开异常忽略
                }

                slot.Connection = null;
            }
        }
        finally
        {
            slot.Gate.Release();
        }

        lock (_primaryLock)
        {
            if (string.Equals(_primaryEndpoint, endpoint, StringComparison.OrdinalIgnoreCase))
            {
                _primaryEndpoint = string.Empty;
            }
        }
    }

    private async Task DisconnectAllAsync(CancellationToken ct)
    {
        foreach (var endpoint in _slots.Keys.ToList())
        {
            await DisconnectEndpointAsync(endpoint, ct).ConfigureAwait(false);
        }
    }

    public virtual async Task ShutdownAsync(CancellationToken cancellationToken = default)
    {
        await DisconnectAllAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 从请求参数中解析通信端点（子类可覆盖）
    /// </summary>
    protected virtual string ResolveEndpoint(StepExecutionRequest request)
    {
        if (request.Parameters.TryGetValue("Endpoint", out var ep) && ep != null)
        {
            return ep.ToString()!;
        }

        if (request.Parameters.TryGetValue("SerialPort", out var sp) && sp != null)
        {
            return sp.ToString()!;
        }

        if (request.Parameters.TryGetValue("Host", out var host) && host != null)
        {
            var port = request.Parameters.TryGetValue("Port", out var p) ? p?.ToString() : null;
            return port != null ? $"{host}:{port}" : host.ToString()!;
        }

        if (request.Parameters.TryGetValue("TargetDeviceId", out var deviceId) && deviceId != null)
        {
            return deviceId.ToString()!;
        }

        return string.Empty;
    }

    private static string? TryGetExpectedExpression(IReadOnlyDictionary<string, object?> parameters)
    {
        if (parameters.TryGetValue("ExpectedResult", out var expected) ||
            parameters.TryGetValue("Expected", out expected))
        {
            return expected?.ToString();
        }

        return null;
    }

    /// <summary>
    /// 输出后处理（子类可覆盖，用于清洗输出）
    /// </summary>
    protected virtual string PostProcessOutput(string output, StepExecutionRequest request)
    {
        return output;
    }

    /// <summary>
    /// 判断实际输出是否满足期望表达式，统一路由到 <see cref="ExpectedResultMatcher"/>。
    /// </summary>
    protected static bool IsExpectedResult(string actual, string expectedExpression)
    {
        return ExpectedResultMatcher.Match(expectedExpression, actual);
    }

    /// <summary>
    /// 判断实际输出是否满足期望表达式，并输出失败原因。
    /// </summary>
    protected static bool IsExpectedResult(string actual, string expectedExpression, out string reason)
    {
        return ExpectedResultMatcher.Match(expectedExpression, actual, out reason);
    }

    /// <summary>
    /// 创建并打开指定端点的连接句柄。失败返回 null。
    /// 连接对象由基类按端点缓存；同一端点并发访问由槽位锁串行化。
    /// </summary>
    protected abstract Task<object?> CreateConnectionAsync(string endpoint, CancellationToken ct);

    /// <summary>
    /// 在已打开的连接句柄上发送命令。
    /// </summary>
    protected abstract Task<string> SendCommandOnConnectionAsync(object connection, string command, CancellationToken ct);

    /// <summary>
    /// 关闭并释放连接句柄。
    /// </summary>
    protected abstract Task CloseConnectionAsync(object connection, CancellationToken ct);

    protected StepExecutionResult BuildResult(
        StepExecutionStatus status,
        DateTime startTime,
        string rawOutput = "",
        string errorCode = "",
        string errorMessage = "")
    {
        return new StepExecutionResult
        {
            Status = status,
            StartTimeUtc = startTime,
            EndTimeUtc = DateTime.UtcNow,
            RawOutput = rawOutput,
            NormalizedOutput = rawOutput,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
            PluginId = Metadata.PluginId,
            PluginVersion = Metadata.Version
        };
    }

    private EndpointSlot GetOrCreateSlot(string endpoint)
    {
        return _slots.GetOrAdd(endpoint ?? string.Empty, _ => new EndpointSlot());
    }

    private void SetPrimaryEndpoint(string endpoint)
    {
        lock (_primaryLock)
        {
            _primaryEndpoint = endpoint ?? string.Empty;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 异步释放：优雅地等待断开连接完成。
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    private async ValueTask DisposeAsyncCore()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            await DisconnectAllAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
            // best effort
        }

        DisposeManagedResources();
        DisposeSlots();
        _disposed = true;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed || !disposing)
        {
            return;
        }

        try
        {
            Task.Run(() => DisconnectAllAsync(CancellationToken.None)).Wait(TimeSpan.FromMilliseconds(2000));
        }
        catch
        {
            // 超时或异常忽略
        }

        DisposeManagedResources();
        DisposeSlots();
        _disposed = true;
    }

    private void DisposeSlots()
    {
        foreach (var slot in _slots.Values)
        {
            slot.Gate.Dispose();
        }

        _slots.Clear();
    }

    /// <summary>
    /// 释放托管资源（子类可覆盖）。在同步与异步释放路径中均被调用。
    /// </summary>
    protected virtual void DisposeManagedResources()
    {
    }

    private sealed class EndpointSlot
    {
        public SemaphoreSlim Gate { get; } = new(1, 1);
        public object? Connection { get; set; }
    }
}
