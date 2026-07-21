using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UTF.Plugin.Abstractions;

namespace UTF.Plugins.Drivers;

/// <summary>
/// ADB 通信驱动插件 - 通过 Android Debug Bridge 与 Android 设备通信。
/// 每个设备序列号作为独立端点，支持多设备并行。
/// </summary>
public sealed class AdbDriverPlugin : DeviceDriverPluginBase
{
    private string _adbPath = "adb";

    public override PluginMetadata Metadata { get; } = new()
    {
        PluginId = "utf.driver.adb",
        Name = "UTF ADB Driver",
        Version = "1.0.0",
        PluginApiVersion = PluginApiVersions.V1,
        SupportedStepTypes = new[] { "adb", "android", "shell" },
        SupportedChannels = new[] { "adb", "android", "usb" },
        Priority = 10
    };

    protected override void OnInitialize(PluginInitContext context)
    {
        if (context.Settings.TryGetValue("AdbPath", out var adbPath) && !string.IsNullOrWhiteSpace(adbPath))
        {
            _adbPath = adbPath;
        }
    }

    public override bool CanHandle(string stepType, string channel)
    {
        var supportedTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "adb", "android", "shell"
        };
        var supportedChannels = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "adb", "android", "usb"
        };
        return DefaultCanHandle(stepType ?? string.Empty, channel ?? string.Empty, supportedTypes, supportedChannels);
    }

    protected override string ResolveEndpoint(StepExecutionRequest request)
    {
        if (request.Parameters.TryGetValue("DeviceSerial", out var serial) && serial != null)
        {
            return serial.ToString()!;
        }

        if (request.Parameters.TryGetValue("AdbSerial", out var adbSerial) && adbSerial != null)
        {
            return adbSerial.ToString()!;
        }

        return base.ResolveEndpoint(request);
    }

    protected override async Task<object?> CreateConnectionAsync(string endpoint, CancellationToken ct)
    {
        // 网络端点需要 adb connect；USB 序列号只需在线校验。
        if (IsNetworkEndpoint(endpoint))
        {
            var result = await RunAdbCommandAsync(new[] { "connect", endpoint }, ct).ConfigureAwait(false);
            if (!result.Contains("connected", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
        }
        else if (!string.IsNullOrWhiteSpace(endpoint))
        {
            var devices = await RunAdbCommandAsync(new[] { "devices" }, ct).ConfigureAwait(false);
            if (!devices.Contains(endpoint, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
        }

        return new AdbConnection(endpoint, _adbPath);
    }

    protected override Task<string> SendCommandOnConnectionAsync(object connection, string command, CancellationToken ct)
    {
        var state = (AdbConnection)connection;
        return state.SendCommandAsync(command, ct);
    }

    protected override async Task CloseConnectionAsync(object connection, CancellationToken ct)
    {
        if (connection is AdbConnection state)
        {
            await state.DisconnectAsync(ct).ConfigureAwait(false);
        }
    }

    private static bool IsNetworkEndpoint(string endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return false;
        }

        var hostPortMatch = System.Text.RegularExpressions.Regex.Match(
            endpoint, @"^[^:/]+:\d+$", System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        if (hostPortMatch.Success)
        {
            return true;
        }

        if (endpoint.Contains('.') && Uri.TryCreate($"tcp://{endpoint}", UriKind.Absolute, out _))
        {
            return true;
        }

        return false;
    }

    private async Task<string> RunAdbCommandAsync(IReadOnlyList<string> args, CancellationToken ct)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _adbPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            throw new InvalidOperationException($"启动 ADB 进程失败: {_adbPath} {string.Join(' ', args)}");
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);

        try
        {
            await process.WaitForExitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            TryKillProcessTree(process);
            throw;
        }

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        if (process.ExitCode != 0 && !string.IsNullOrWhiteSpace(stderr))
        {
            throw new InvalidOperationException($"ADB 命令失败 (exit={process.ExitCode}): {stderr.Trim()}");
        }

        return $"{stdout}{Environment.NewLine}{stderr}".Trim();
    }

    private static void TryKillProcessTree(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(2000);
            }
        }
        catch
        {
            // Cancellation must still propagate even if the OS refuses termination.
        }
    }

    private sealed class AdbConnection
    {
        private readonly string _deviceSerial;
        private readonly string _adbPath;

        public AdbConnection(string deviceSerial, string adbPath)
        {
            _deviceSerial = deviceSerial ?? string.Empty;
            _adbPath = adbPath;
        }

        public async Task<string> SendCommandAsync(string command, CancellationToken ct)
        {
            var trimmedCommand = command.Trim();

            if (trimmedCommand.StartsWith("adb ", StringComparison.OrdinalIgnoreCase))
            {
                var adbArgs = trimmedCommand.Substring(4).Trim();
                if (!string.IsNullOrWhiteSpace(_deviceSerial))
                {
                    return await RunAsync(new[] { "-s", _deviceSerial, adbArgs }, ct).ConfigureAwait(false);
                }

                return await RunAsync(new[] { adbArgs }, ct).ConfigureAwait(false);
            }

            if (!string.IsNullOrWhiteSpace(_deviceSerial))
            {
                return await RunAsync(new[] { "-s", _deviceSerial, "shell", trimmedCommand }, ct).ConfigureAwait(false);
            }

            return await RunAsync(new[] { "shell", trimmedCommand }, ct).ConfigureAwait(false);
        }

        public async Task DisconnectAsync(CancellationToken ct)
        {
            if (IsNetworkEndpoint(_deviceSerial))
            {
                await RunAsync(new[] { "disconnect", _deviceSerial }, ct).ConfigureAwait(false);
            }
        }

        private async Task<string> RunAsync(IReadOnlyList<string> args, CancellationToken ct)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = _adbPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            foreach (var arg in args)
            {
                startInfo.ArgumentList.Add(arg);
            }

            using var process = new Process { StartInfo = startInfo };
            if (!process.Start())
            {
                throw new InvalidOperationException($"启动 ADB 进程失败: {_adbPath}");
            }

            var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
            var stderrTask = process.StandardError.ReadToEndAsync(ct);

            try
            {
                await process.WaitForExitAsync(ct);
            }
            catch (OperationCanceledException)
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                    }
                }
                catch
                {
                    // ignore
                }

                throw;
            }

            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            if (process.ExitCode != 0 && !string.IsNullOrWhiteSpace(stderr))
            {
                throw new InvalidOperationException($"ADB 命令失败 (exit={process.ExitCode}): {stderr.Trim()}");
            }

            return $"{stdout}{Environment.NewLine}{stderr}".Trim();
        }
    }
}
