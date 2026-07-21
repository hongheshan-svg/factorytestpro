using System.Diagnostics;
using UTF.Plugin.Abstractions;

namespace UTF.Plugins.Example;

public sealed class CmdStepExecutorPlugin : IStepExecutorPlugin
{
    private PluginMetadata _metadata = new()
    {
        PluginId = "utf.executor.cmd",
        Name = "UTF Cmd Executor",
        Version = "1.0.0",
        PluginApiVersion = PluginApiVersions.V1,
        SupportedStepTypes = new[] { "custom", "command" },
        SupportedChannels = new[] { "cmd", "command", "powershell", "ps" },
        Priority = 100
    };

    public PluginMetadata Metadata => _metadata;

    public Task InitializeAsync(PluginInitContext context, CancellationToken cancellationToken = default)
    {
        if (!string.Equals(context.PluginApiVersion, PluginApiVersions.V1, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"不支持的插件 API 版本: {context.PluginApiVersion}，当前插件仅支持 {PluginApiVersions.V1}");
        }

        return Task.CompletedTask;
    }

    public bool CanHandle(string stepType, string channel)
    {
        var normalizedType = (stepType ?? string.Empty).Trim().ToLowerInvariant();
        var normalizedChannel = (channel ?? string.Empty).Trim().ToLowerInvariant();

        var typeMatch = normalizedType is "custom" or "command";
        var channelMatch = normalizedChannel is "cmd" or "command" or "powershell" or "ps";
        return typeMatch && channelMatch;
    }

    public async Task<StepExecutionResult> ExecuteAsync(StepExecutionRequest request, CancellationToken cancellationToken = default)
    {
        var startedAt = DateTime.UtcNow;
        Process? process = null;
        try
        {
            if (string.IsNullOrWhiteSpace(request.Command))
            {
                return new StepExecutionResult
                {
                    Status = StepExecutionStatus.Failed,
                    StartTimeUtc = startedAt,
                    EndTimeUtc = DateTime.UtcNow,
                    ErrorCode = "PLG_CMD_001",
                    ErrorMessage = "命令为空，无法执行。",
                    PluginId = Metadata.PluginId,
                    PluginVersion = Metadata.Version
                };
            }

            var startInfo = BuildProcessStartInfo(request.Channel, request.Command);

            process = new Process { StartInfo = startInfo };
            if (!process.Start())
            {
                return new StepExecutionResult
                {
                    Status = StepExecutionStatus.Error,
                    StartTimeUtc = startedAt,
                    EndTimeUtc = DateTime.UtcNow,
                    ErrorCode = "PLG_CMD_002",
                    ErrorMessage = "启动外部命令失败。",
                    PluginId = Metadata.PluginId,
                    PluginVersion = Metadata.Version
                };
            }

            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

            var stdout = await outputTask.ConfigureAwait(false);
            var stderr = await errorTask.ConfigureAwait(false);
            var merged = $"{stdout}{Environment.NewLine}{stderr}".Trim();

            if (process.ExitCode != 0)
            {
                return new StepExecutionResult
                {
                    Status = StepExecutionStatus.Failed,
                    StartTimeUtc = startedAt,
                    EndTimeUtc = DateTime.UtcNow,
                    RawOutput = merged,
                    NormalizedOutput = merged,
                    ErrorCode = "PLG_CMD_003",
                    ErrorMessage = $"命令返回非 0 退出码: {process.ExitCode}",
                    PluginId = Metadata.PluginId,
                    PluginVersion = Metadata.Version
                };
            }

            var expected = TryGetExpectedExpression(request.Parameters);
            if (!string.IsNullOrWhiteSpace(expected) &&
                !IsExpectedResult(merged, expected!, out var reason))
            {
                return new StepExecutionResult
                {
                    Status = StepExecutionStatus.Failed,
                    StartTimeUtc = startedAt,
                    EndTimeUtc = DateTime.UtcNow,
                    RawOutput = merged,
                    NormalizedOutput = merged,
                    ErrorCode = "PLG_CMD_004",
                    ErrorMessage = reason,
                    PluginId = Metadata.PluginId,
                    PluginVersion = Metadata.Version
                };
            }

            return new StepExecutionResult
            {
                Status = StepExecutionStatus.Passed,
                StartTimeUtc = startedAt,
                EndTimeUtc = DateTime.UtcNow,
                RawOutput = merged,
                NormalizedOutput = merged,
                PluginId = Metadata.PluginId,
                PluginVersion = Metadata.Version
            };
        }
        catch (OperationCanceledException)
        {
            TryKillProcessTree(process);
            throw;
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
                PluginId = Metadata.PluginId,
                PluginVersion = Metadata.Version
            };
        }
        finally
        {
            process?.Dispose();
        }
    }

    public Task ShutdownAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// 构建 <see cref="ProcessStartInfo"/>，使用 ArgumentList 逐参数添加，避免手动转义导致的命令注入。
    /// 始终设置 UseShellExecute=false 与重定向输出。
    /// </summary>
    private static ProcessStartInfo BuildProcessStartInfo(string channel, string command)
    {
        var normalizedChannel = (channel ?? string.Empty).Trim().ToLowerInvariant();
        var startInfo = new ProcessStartInfo
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        if (normalizedChannel is "powershell" or "ps")
        {
            startInfo.FileName = "powershell.exe";
            // 逐参数添加，框架负责正确转义；命令原样作为单个参数，不做手动转义。
            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-NonInteractive");
            startInfo.ArgumentList.Add("-Command");
            startInfo.ArgumentList.Add(command);
        }
        else
        {
            startInfo.FileName = "cmd.exe";
            startInfo.ArgumentList.Add("/c");
            startInfo.ArgumentList.Add(command);
        }

        return startInfo;
    }

    private static string? TryGetExpectedExpression(IReadOnlyDictionary<string, object?> parameters)
    {
        if (parameters.TryGetValue("ExpectedResult", out var expected))
        {
            return expected?.ToString();
        }

        if (parameters.TryGetValue("Expected", out expected))
        {
            return expected?.ToString();
        }

        return null;
    }

    private static bool IsExpectedResult(string response, string expectedExpression, out string reason)
    {
        return ExpectedResultMatcher.Match(expectedExpression, response, out reason);
    }

    private static void TryKillProcessTree(Process? process)
    {
        try
        {
            if (process is { HasExited: false })
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(2000);
            }
        }
        catch
        {
            // Preserve the original cancellation signal.
        }
    }
}
