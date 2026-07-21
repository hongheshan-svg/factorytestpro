using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

namespace UTF.Plugin.Abstractions;

/// <summary>
/// 设备驱动插件基类 - 提供通用的连接管理、超时控制和结果封装
/// 子类只需实现 ConnectCoreAsync / SendCommandCoreAsync / DisconnectCoreAsync
/// </summary>
public abstract class DeviceDriverPluginBase : IStepExecutorPlugin, IDeviceDriverPlugin, IDisposable, IAsyncDisposable
{
    private bool _isConnected;
    private string _currentEndpoint = string.Empty;
    private readonly SemaphoreSlim _executionLock = new(1, 1);
    private bool _disposed;

    /// <summary>
    /// 当前是否已连接
    /// </summary>
    protected bool IsConnected => _isConnected;

    /// <summary>
    /// 当前连接的端点
    /// </summary>
    protected string CurrentEndpoint => _currentEndpoint;

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
    /// 子类应优先调用 <see cref="DefaultCanHandle"/> 实现统一语义。
    /// </summary>
    public abstract bool CanHandle(string stepType, string channel);

    /// <summary>
    /// 统一的 AND 语义匹配辅助方法。当 <paramref name="supportedTypes"/> 或
    /// <paramref name="supportedChannels"/> 任一集合包含 <c>"*"</c> 时，对应侧恒匹配；
    /// 否则要求请求值出现在对应集合中（忽略大小写）。
    /// </summary>
    /// <param name="stepType">请求的步骤类型。</param>
    /// <param name="channel">请求的通道。</param>
    /// <param name="supportedTypes">本插件支持的步骤类型集合（可含 <c>"*"</c>）。</param>
    /// <param name="supportedChannels">本插件支持的通道集合（可含 <c>"*"</c>）。</param>
    /// <returns>两侧均匹配返回 true。</returns>
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
        await _executionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        var startedAt = DateTime.UtcNow;
        try
        {
            if (string.IsNullOrWhiteSpace(request.Command))
            {
                return BuildResult(StepExecutionStatus.Failed, startedAt,
                    errorCode: "PLG_DRV_001", errorMessage: "命令为空，无法执行。");
            }

            var endpoint = ResolveEndpoint(request);

            // 自动连接管理
            if (!_isConnected || !string.Equals(_currentEndpoint, endpoint, StringComparison.OrdinalIgnoreCase))
            {
                if (_isConnected)
                {
                    await DisconnectAsync(cancellationToken).ConfigureAwait(false);
                }

                if (!string.IsNullOrWhiteSpace(endpoint))
                {
                    var connected = await ConnectAsync(endpoint, cancellationToken).ConfigureAwait(false);
                    if (!connected)
                    {
                        return BuildResult(StepExecutionStatus.Failed, startedAt,
                            errorCode: "PLG_DRV_002",
                            errorMessage: $"连接端点失败: {endpoint}");
                    }
                }
            }

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(request.TimeoutMs);

            var output = await SendCommandAsync(request.Command, timeoutCts.Token).ConfigureAwait(false);

            // 执行后处理（子类可覆盖）
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
        finally
        {
            _executionLock.Release();
        }
    }

    public async Task<bool> ConnectAsync(string endpoint, CancellationToken ct = default)
    {
        if (_isConnected && string.Equals(_currentEndpoint, endpoint, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (_isConnected)
        {
            await DisconnectCoreAsync(ct).ConfigureAwait(false);
            _isConnected = false;
            _currentEndpoint = string.Empty;
        }

        var result = await ConnectCoreAsync(endpoint, ct).ConfigureAwait(false);
        if (result)
        {
            _isConnected = true;
            _currentEndpoint = endpoint;
        }

        return result;
    }

    public async Task<string> SendCommandAsync(string command, CancellationToken ct = default)
    {
        return await SendCommandCoreAsync(command, ct).ConfigureAwait(false);
    }

    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        if (_isConnected)
        {
            await DisconnectCoreAsync(ct).ConfigureAwait(false);
            _isConnected = false;
            _currentEndpoint = string.Empty;
        }
    }

    public virtual async Task ShutdownAsync(CancellationToken cancellationToken = default)
    {
        await _executionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await DisconnectAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _executionLock.Release();
        }
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
    /// 支持 <c>contains:</c>/<c>equals:</c>/<c>regex:</c>/<c>notcontains:</c> 及裸文本。
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
    /// 核心连接实现 - 子类必须实现
    /// </summary>
    protected abstract Task<bool> ConnectCoreAsync(string endpoint, CancellationToken ct);

    /// <summary>
    /// 核心命令发送实现 - 子类必须实现
    /// </summary>
    protected abstract Task<string> SendCommandCoreAsync(string command, CancellationToken ct);

    /// <summary>
    /// 核心断开连接实现 - 子类必须实现
    /// </summary>
    protected abstract Task DisconnectCoreAsync(CancellationToken ct);

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

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 异步释放：优雅地等待断开连接完成。优先使用此方法以避免 sync-over-async。
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

        if (_isConnected)
        {
            try
            {
                await DisconnectCoreAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch
            {
                // 断开异常忽略，确保释放流程完成
            }

            _isConnected = false;
        }

        DisposeManagedResources();
        _executionLock.Dispose();
        _disposed = true;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed || !disposing)
        {
            return;
        }

        if (_isConnected)
        {
            // 不直接 await DisconnectCoreAsync（sync-over-async 死锁风险）；
            // 在同步 Dispose 中以 2 秒硬超时执行断开，超时即放弃，由终结器/异步路径兜底。
            try
            {
                Task.Run(() => DisconnectCoreAsync(CancellationToken.None)).Wait(TimeSpan.FromMilliseconds(2000));
            }
            catch
            {
                // 超时或异常忽略
            }

            _isConnected = false;
        }

        DisposeManagedResources();
        _executionLock.Dispose();
        _disposed = true;
    }

    /// <summary>
    /// 释放托管资源（子类可覆盖以释放端口/连接等）。在同步与异步释放路径中均被调用。
    /// </summary>
    protected virtual void DisposeManagedResources()
    {
    }
}
