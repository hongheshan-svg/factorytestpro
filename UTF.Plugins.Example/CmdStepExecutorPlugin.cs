using System.Diagnostics;
using System.Text.RegularExpressions;
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

            var (fileName, arguments) = BuildProcessStartInfo(request.Channel, request.Command);
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };
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

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync(cancellationToken);

            var stdout = await outputTask;
            var stderr = await errorTask;
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
            return new StepExecutionResult
            {
                Status = StepExecutionStatus.Timeout,
                StartTimeUtc = startedAt,
                EndTimeUtc = DateTime.UtcNow,
                ErrorCode = PluginErrorCodes.ExecuteTimeout,
                ErrorMessage = "插件命令执行超时。",
                PluginId = Metadata.PluginId,
                PluginVersion = Metadata.Version
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
                PluginId = Metadata.PluginId,
                PluginVersion = Metadata.Version
            };
        }
    }

    public Task ShutdownAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    private static (string FileName, string Arguments) BuildProcessStartInfo(string channel, string command)
    {
        var normalizedChannel = (channel ?? string.Empty).Trim().ToLowerInvariant();
        if (normalizedChannel is "powershell" or "ps")
        {
            var escaped = command.Replace("\"", "\\\"");
            return ("powershell.exe", $"-NoProfile -NonInteractive -Command \"{escaped}\"");
        }

        return ("cmd.exe", $"/c {command}");
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
        reason = string.Empty;
        var text = response ?? string.Empty;
        var expression = expectedExpression.Trim();

        if (string.IsNullOrEmpty(expression))
        {
            return true;
        }

        if (expression.StartsWith("contains:", StringComparison.OrdinalIgnoreCase))
        {
            var expected = expression[9..];
            var ok = text.Contains(expected, StringComparison.OrdinalIgnoreCase);
            if (!ok)
            {
                reason = $"响应不包含预期内容: {expected}";
            }
            return ok;
        }

        if (expression.StartsWith("equals:", StringComparison.OrdinalIgnoreCase))
        {
            var expected = expression[7..];
            var ok = string.Equals(text.Trim(), expected.Trim(), StringComparison.OrdinalIgnoreCase);
            if (!ok)
            {
                reason = $"响应与预期不一致，预期: {expected}";
            }
            return ok;
        }

        if (expression.StartsWith("regex:", StringComparison.OrdinalIgnoreCase))
        {
            var pattern = expression[6..];
            var ok = Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (!ok)
            {
                reason = $"响应不匹配正则: {pattern}";
            }
            return ok;
        }

        var fallback = text.Contains(expression, StringComparison.OrdinalIgnoreCase);
        if (!fallback)
        {
            reason = $"响应不包含预期文本: {expression}";
        }
        return fallback;
    }
}
