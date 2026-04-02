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
public abstract class DeviceDriverPluginBase : IStepExecutorPlugin, IDeviceDriverPlugin, IDisposable
{
    private bool _isConnected;
    private string _currentEndpoint = string.Empty;
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

    public abstract bool CanHandle(string stepType, string channel);

    public async Task<StepExecutionResult> ExecuteAsync(StepExecutionRequest request, CancellationToken cancellationToken = default)
    {
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
                    await DisconnectAsync(cancellationToken);
                }

                if (!string.IsNullOrWhiteSpace(endpoint))
                {
                    var connected = await ConnectAsync(endpoint, cancellationToken);
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

            var output = await SendCommandAsync(request.Command, timeoutCts.Token);

            // 执行后处理（子类可覆盖）
            output = PostProcessOutput(output, request);

            return BuildResult(StepExecutionStatus.Passed, startedAt, rawOutput: output);
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

    public async Task<bool> ConnectAsync(string endpoint, CancellationToken ct = default)
    {
        if (_isConnected && string.Equals(_currentEndpoint, endpoint, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (_isConnected)
        {
            await DisconnectCoreAsync(ct);
            _isConnected = false;
            _currentEndpoint = string.Empty;
        }

        var result = await ConnectCoreAsync(endpoint, ct);
        if (result)
        {
            _isConnected = true;
            _currentEndpoint = endpoint;
        }

        return result;
    }

    public async Task<string> SendCommandAsync(string command, CancellationToken ct = default)
    {
        return await SendCommandCoreAsync(command, ct);
    }

    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        if (_isConnected)
        {
            await DisconnectCoreAsync(ct);
            _isConnected = false;
            _currentEndpoint = string.Empty;
        }
    }

    public virtual async Task ShutdownAsync(CancellationToken cancellationToken = default)
    {
        await DisconnectAsync(cancellationToken);
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

        return string.Empty;
    }

    /// <summary>
    /// 输出后处理（子类可覆盖，用于清洗输出）
    /// </summary>
    protected virtual string PostProcessOutput(string output, StepExecutionRequest request)
    {
        return output;
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

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            if (_isConnected)
            {
                DisconnectCoreAsync(CancellationToken.None).GetAwaiter().GetResult();
                _isConnected = false;
            }

            _disposed = true;
        }
    }
}
