using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UTF.Plugin.Abstractions;

namespace UTF.Plugins.Drivers;

/// <summary>
/// ADB 通信驱动插件 - 通过 Android Debug Bridge 与 Android 设备通信
/// 支持 adb shell、adb push/pull、adb install 等命令
/// </summary>
public sealed class AdbDriverPlugin : DeviceDriverPluginBase
{
    private string _adbPath = "adb";
    private string _currentDeviceSerial = string.Empty;

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
        var normalizedType = (stepType ?? string.Empty).Trim().ToLowerInvariant();
        var normalizedChannel = (channel ?? string.Empty).Trim().ToLowerInvariant();
        return normalizedType is "adb" or "android" or "shell"
            || normalizedChannel is "adb" or "android" or "usb";
    }

    protected override string ResolveEndpoint(StepExecutionRequest request)
    {
        // ADB 设备通过 serial number 标识（USB 或 IP:Port）
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

    protected override async Task<bool> ConnectCoreAsync(string endpoint, CancellationToken ct)
    {
        _currentDeviceSerial = endpoint;

        // 如果是 IP:Port 形式，需要先执行 adb connect
        if (endpoint.Contains(':') || endpoint.Contains('.'))
        {
            var result = await RunAdbCommandAsync($"connect {endpoint}", ct);
            return result.Contains("connected", StringComparison.OrdinalIgnoreCase);
        }

        // USB 连接的设备，验证设备是否在线
        var devices = await RunAdbCommandAsync("devices", ct);
        return devices.Contains(endpoint, StringComparison.OrdinalIgnoreCase);
    }

    protected override async Task<string> SendCommandCoreAsync(string command, CancellationToken ct)
    {
        var trimmedCommand = command.Trim();

        // 判断是否需要自动包装为 adb shell 命令
        if (trimmedCommand.StartsWith("adb ", StringComparison.OrdinalIgnoreCase))
        {
            // 用户已经提供了完整的 adb 命令（去掉 adb 前缀）
            var adbArgs = trimmedCommand.Substring(4).Trim();
            if (!string.IsNullOrWhiteSpace(_currentDeviceSerial))
            {
                return await RunAdbCommandAsync($"-s {_currentDeviceSerial} {adbArgs}", ct);
            }

            return await RunAdbCommandAsync(adbArgs, ct);
        }

        // 默认包装为 adb shell 命令
        if (!string.IsNullOrWhiteSpace(_currentDeviceSerial))
        {
            return await RunAdbCommandAsync($"-s {_currentDeviceSerial} shell {trimmedCommand}", ct);
        }

        return await RunAdbCommandAsync($"shell {trimmedCommand}", ct);
    }

    protected override async Task DisconnectCoreAsync(CancellationToken ct)
    {
        if (_currentDeviceSerial.Contains(':') || _currentDeviceSerial.Contains('.'))
        {
            await RunAdbCommandAsync($"disconnect {_currentDeviceSerial}", ct);
        }

        _currentDeviceSerial = string.Empty;
    }

    private async Task<string> RunAdbCommandAsync(string arguments, CancellationToken ct)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _adbPath,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            throw new InvalidOperationException($"启动 ADB 进程失败: {_adbPath} {arguments}");
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);

        await process.WaitForExitAsync(ct);

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        if (process.ExitCode != 0 && !string.IsNullOrWhiteSpace(stderr))
        {
            throw new InvalidOperationException($"ADB 命令失败 (exit={process.ExitCode}): {stderr.Trim()}");
        }

        return $"{stdout}{Environment.NewLine}{stderr}".Trim();
    }
}
