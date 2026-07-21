using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace UTF.HAL;

/// <summary>
/// DUT通信助手类，支持串口和CMD通信
/// </summary>
[Obsolete("Logic moved to UTF.Business step execution / UTF.Plugins.Drivers", error: true)]
public static class DUTCommunicationHelper
{
    /// <summary>
    /// 执行串口命令
    /// </summary>
    /// <param name="portName">串口名称</param>
    /// <param name="baudRate">波特率</param>
    /// <param name="command">要发送的命令</param>
    /// <param name="timeoutMs">超时时间（毫秒）</param>
    /// <param name="terminator">结束符</param>
    /// <returns>执行结果</returns>
    public static async Task<DUTCommunicationResult> ExecuteSerialCommandAsync(
        string portName, 
        int baudRate, 
        string command, 
        int timeoutMs = 5000,
        string terminator = "\r\n")
    {
        var startTime = DateTime.UtcNow;
        SerialPort? serialPort = null;

        try
        {
            serialPort = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One)
            {
                ReadTimeout = timeoutMs,
                WriteTimeout = timeoutMs,
                NewLine = terminator
            };

            serialPort.Open();
            
            // 发送命令
            serialPort.WriteLine(command);
            
            // 读取响应
            var response = await Task.Run(() => serialPort.ReadLine()).ConfigureAwait(false);
            
            return new DUTCommunicationResult
            {
                Success = true,
                Command = command,
                Response = response,
                ExecutionTime = DateTime.UtcNow - startTime,
                CommunicationType = "Serial"
            };
        }
        catch (Exception ex)
        {
            return new DUTCommunicationResult
            {
                Success = false,
                Command = command,
                ErrorMessage = ex.Message,
                ExecutionTime = DateTime.UtcNow - startTime,
                CommunicationType = "Serial"
            };
        }
        finally
        {
            serialPort?.Close();
            serialPort?.Dispose();
        }
    }

    /// <summary>
    /// 执行CMD命令
    /// </summary>
    /// <param name="command">要执行的命令</param>
    /// <param name="workingDirectory">工作目录</param>
    /// <param name="timeoutMs">超时时间（毫秒）</param>
    /// <returns>执行结果</returns>
    public static async Task<DUTCommunicationResult> ExecuteCmdCommandAsync(
        string command,
        string? workingDirectory = null,
        int timeoutMs = 30000)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            using var process = new Process();
            var startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                // 使用 ArgumentList 逐参数添加，避免手动拼接导致的命令注入。
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add("/c");
            startInfo.ArgumentList.Add(command);

            // 未显式指定工作目录时使用当前进程目录，不再默认 "C:\\"（避免意外写入根盘符）。
            if (!string.IsNullOrWhiteSpace(workingDirectory))
            {
                startInfo.WorkingDirectory = workingDirectory;
            }

            process.StartInfo = startInfo;

            process.Start();

            using var cancellationTokenSource = new CancellationTokenSource(timeoutMs);

            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationTokenSource.Token);
            var errorTask = process.StandardError.ReadToEndAsync(cancellationTokenSource.Token);

            await process.WaitForExitAsync(cancellationTokenSource.Token).ConfigureAwait(false);

            var output = await outputTask.ConfigureAwait(false);
            var error = await errorTask.ConfigureAwait(false);
            
            var success = process.ExitCode == 0;
            var response = success ? output : error;

            return new DUTCommunicationResult
            {
                Success = success,
                Command = command,
                Response = response.Trim(),
                ExecutionTime = DateTime.UtcNow - startTime,
                CommunicationType = "CMD",
                ExitCode = process.ExitCode
            };
        }
        catch (Exception ex)
        {
            return new DUTCommunicationResult
            {
                Success = false,
                Command = command,
                ErrorMessage = ex.Message,
                ExecutionTime = DateTime.UtcNow - startTime,
                CommunicationType = "CMD"
            };
        }
    }

}

/// <summary>
/// DUT通信结果
/// </summary>
public sealed record DUTCommunicationResult
{
    /// <summary>是否成功</summary>
    public bool Success { get; init; }

    /// <summary>执行的命令</summary>
    public string Command { get; init; } = "";

    /// <summary>响应内容</summary>
    public string Response { get; init; } = "";

    /// <summary>错误信息</summary>
    public string ErrorMessage { get; init; } = "";

    /// <summary>执行时间</summary>
    public TimeSpan ExecutionTime { get; init; }

    /// <summary>通信类型</summary>
    public string CommunicationType { get; init; } = "";

    /// <summary>退出代码（CMD命令使用）</summary>
    public int ExitCode { get; init; }

    /// <summary>附加数据</summary>
    public Dictionary<string, object> AdditionalData { get; init; } = new();
}

/// <summary>
/// DUT通信配置
/// </summary>
public sealed record DUTCommunicationConfig
{
    /// <summary>通信类型</summary>
    public string CommunicationType { get; init; } = "Serial"; // Serial, CMD

    /// <summary>串口配置</summary>
    public DUTSerialConfig? SerialConfig { get; init; }

    /// <summary>CMD配置</summary>
    public DUTCmdConfig? CmdConfig { get; init; }

    /// <summary>默认超时时间（毫秒）</summary>
    public int DefaultTimeoutMs { get; init; } = 5000;

    /// <summary>重试次数</summary>
    public int RetryCount { get; init; } = 3;

    /// <summary>重试延迟（毫秒）</summary>
    public int RetryDelayMs { get; init; } = 1000;
}

/// <summary>
/// DUT串口配置
/// </summary>
public sealed record DUTSerialConfig
{
    /// <summary>端口名称</summary>
    public string PortName { get; init; } = "COM1";

    /// <summary>波特率</summary>
    public int BaudRate { get; init; } = 115200;

    /// <summary>数据位</summary>
    public int DataBits { get; init; } = 8;

    /// <summary>停止位</summary>
    public StopBits StopBits { get; init; } = StopBits.One;

    /// <summary>奇偶校验</summary>
    public Parity Parity { get; init; } = Parity.None;

    /// <summary>流控制</summary>
    public Handshake Handshake { get; init; } = Handshake.None;

    /// <summary>读取超时（毫秒）</summary>
    public int ReadTimeoutMs { get; init; } = 5000;

    /// <summary>写入超时（毫秒）</summary>
    public int WriteTimeoutMs { get; init; } = 5000;

    /// <summary>行结束符</summary>
    public string NewLine { get; init; } = "\r\n";

    /// <summary>DTR使能</summary>
    public bool DtrEnable { get; init; } = false;

    /// <summary>RTS使能</summary>
    public bool RtsEnable { get; init; } = false;
}

/// <summary>
/// DUT CMD配置
/// </summary>
public sealed record DUTCmdConfig
{
    /// <summary>工作目录</summary>
    public string WorkingDirectory { get; init; } = "C:\\";

    /// <summary>命令超时时间（毫秒）</summary>
    public int CommandTimeoutMs { get; init; } = 30000;

    /// <summary>使用PowerShell而不是CMD</summary>
    public bool UsePowerShell { get; init; } = false;

    /// <summary>环境变量</summary>
    public Dictionary<string, string> EnvironmentVariables { get; init; } = new();

    /// <summary>初始化命令</summary>
    public List<string> InitializationCommands { get; init; } = new();

    /// <summary>清理命令</summary>
    public List<string> CleanupCommands { get; init; } = new();
}
