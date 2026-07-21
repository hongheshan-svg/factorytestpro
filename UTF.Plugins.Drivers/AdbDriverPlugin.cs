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
        // AND 语义：stepType 与 channel 必须同时匹配。
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

        // 判断是否为网络端点（host:port 或 IP），需要先执行 adb connect；
        // 否则视为 USB 设备序列号。用 Uri/正则判断取代宽松的 Contains(':')/Contains('.')。
        if (IsNetworkEndpoint(endpoint))
        {
            var result = await RunAdbCommandAsync(new[] { "connect", endpoint }, ct).ConfigureAwait(false);
            return result.Contains("connected", StringComparison.OrdinalIgnoreCase);
        }

        // USB 连接的设备，验证设备是否在线
        var devices = await RunAdbCommandAsync(new[] { "devices" }, ct).ConfigureAwait(false);
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
                return await RunAdbCommandAsync(new[] { "-s", _currentDeviceSerial, adbArgs }, ct).ConfigureAwait(false);
            }

            return await RunAdbCommandAsync(new[] { adbArgs }, ct).ConfigureAwait(false);
        }

        // 默认包装为 adb shell 命令
        if (!string.IsNullOrWhiteSpace(_currentDeviceSerial))
        {
            return await RunAdbCommandAsync(new[] { "-s", _currentDeviceSerial, "shell", trimmedCommand }, ct).ConfigureAwait(false);
        }

        return await RunAdbCommandAsync(new[] { "shell", trimmedCommand }, ct).ConfigureAwait(false);
    }

    protected override async Task DisconnectCoreAsync(CancellationToken ct)
    {
        if (IsNetworkEndpoint(_currentDeviceSerial))
        {
            await RunAdbCommandAsync(new[] { "disconnect", _currentDeviceSerial }, ct).ConfigureAwait(false);
        }

        _currentDeviceSerial = string.Empty;
    }

    /// <summary>
    /// 判断端点是否为网络形式（host:port 或点分 IPv4/IPv6/主机名），
    /// 用于区分需要 adb connect 的网络设备与 USB 序列号。
    /// </summary>
    private static bool IsNetworkEndpoint(string endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return false;
        }

        // host:port 形式（host 不含冒号，port 为数字）
        var hostPortMatch = System.Text.RegularExpressions.Regex.Match(
            endpoint, @"^[^:/]+:\d+$", System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        if (hostPortMatch.Success)
        {
            return true;
        }

        // 纯点分 IPv4 / 含点的主机名（USB 序列号通常不含点与冒号）
        if (endpoint.Contains('.') && Uri.TryCreate($"tcp://{endpoint}", UriKind.Absolute, out _))
        {
            return true;
        }

        return false;
    }

    private async Task<string> RunAdbCommandAsync(IReadOnlyList<string> args, CancellationToken ct)
    {
        // 使用 ArgumentList 逐参数添加而非拼接字符串赋值给 Arguments，
        // 以避免因设备序列号/命令文本中混入特殊字符而导致的参数注入。
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
}
