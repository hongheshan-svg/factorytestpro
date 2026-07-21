using System;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UTF.Plugin.Abstractions;

namespace UTF.Plugins.Drivers;

/// <summary>
/// 串口通信驱动插件 - 通过 RS232/RS485 串口与 DUT 通信。
/// 每个串口端点独立连接，支持多 DUT 并行。
/// </summary>
public sealed class SerialDriverPlugin : DeviceDriverPluginBase
{
    private int _baudRate = 115200;
    private int _dataBits = 8;
    private StopBits _stopBits = StopBits.One;
    private Parity _parity = Parity.None;
    private int _readTimeoutMs = 2000;
    private string _lineEnding = "\r\n";

    public override PluginMetadata Metadata { get; } = new()
    {
        PluginId = "utf.driver.serial",
        Name = "UTF Serial Driver",
        Version = "1.0.0",
        PluginApiVersion = PluginApiVersions.V1,
        SupportedStepTypes = new[] { "serial", "uart", "rs232", "rs485" },
        SupportedChannels = new[] { "serial", "uart", "com" },
        Priority = 10
    };

    protected override void OnInitialize(PluginInitContext context)
    {
        if (context.Settings.TryGetValue("BaudRate", out var br) && int.TryParse(br, out var baudRate))
        {
            _baudRate = baudRate;
        }

        if (context.Settings.TryGetValue("DataBits", out var db) && int.TryParse(db, out var dataBits))
        {
            _dataBits = dataBits;
        }

        if (context.Settings.TryGetValue("StopBits", out var sb) && Enum.TryParse<StopBits>(sb, true, out var stopBits))
        {
            _stopBits = stopBits;
        }

        if (context.Settings.TryGetValue("Parity", out var p) && Enum.TryParse<Parity>(p, true, out var parity))
        {
            _parity = parity;
        }

        if (context.Settings.TryGetValue("ReadTimeoutMs", out var rt) && int.TryParse(rt, out var readTimeout))
        {
            _readTimeoutMs = readTimeout;
        }

        if (context.Settings.TryGetValue("LineEnding", out var le) && !string.IsNullOrEmpty(le))
        {
            _lineEnding = le.Replace("\\r", "\r").Replace("\\n", "\n");
        }
    }

    public override bool CanHandle(string stepType, string channel)
    {
        var supportedTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "serial", "uart", "rs232", "rs485"
        };
        var supportedChannels = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "serial", "uart", "com"
        };
        return DefaultCanHandle(stepType ?? string.Empty, channel ?? string.Empty, supportedTypes, supportedChannels);
    }

    protected override string ResolveEndpoint(StepExecutionRequest request)
    {
        if (request.Parameters.TryGetValue("SerialPort", out var sp) && sp != null)
        {
            return sp.ToString()!;
        }

        if (request.Parameters.TryGetValue("Endpoint", out var ep) && ep != null)
        {
            return ep.ToString()!;
        }

        return string.Empty;
    }

    protected override Task<object?> CreateConnectionAsync(string endpoint, CancellationToken ct)
    {
        try
        {
            var serialPort = new SerialPort(endpoint, _baudRate, _parity, _dataBits, _stopBits)
            {
                ReadTimeout = _readTimeoutMs,
                WriteTimeout = _readTimeoutMs,
                Encoding = Encoding.UTF8
            };
            serialPort.Open();
            serialPort.DiscardInBuffer();
            serialPort.DiscardOutBuffer();
            return Task.FromResult<object?>(serialPort);
        }
        catch
        {
            return Task.FromResult<object?>(null);
        }
    }

    protected override async Task<string> SendCommandOnConnectionAsync(object connection, string command, CancellationToken ct)
    {
        var serialPort = (SerialPort)connection;
        if (!serialPort.IsOpen)
        {
            throw new InvalidOperationException("串口未打开");
        }

        var baseStream = serialPort.BaseStream
            ?? throw new InvalidOperationException("串口基础流不可用");
        serialPort.DiscardInBuffer();
        serialPort.Write(command + _lineEnding);

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        linkedCts.CancelAfter(_readTimeoutMs);

        var buffer = new byte[4096];
        var response = new StringBuilder();

        try
        {
            while (!linkedCts.IsCancellationRequested)
            {
                int bytesRead;
                try
                {
                    bytesRead = await baseStream.ReadAsync(buffer.AsMemory(0, buffer.Length), linkedCts.Token)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (linkedCts.IsCancellationRequested && !ct.IsCancellationRequested)
                {
                    break;
                }

                if (bytesRead > 0)
                {
                    response.Append(serialPort.Encoding.GetString(buffer, 0, bytesRead));
                }
                else
                {
                    break;
                }
            }
        }
        catch
        {
            // 串口读取异常：返回已收到的内容
        }

        return response.ToString().Trim();
    }

    protected override Task CloseConnectionAsync(object connection, CancellationToken ct)
    {
        if (connection is SerialPort serialPort)
        {
            if (serialPort.IsOpen)
            {
                serialPort.Close();
            }

            serialPort.Dispose();
        }

        return Task.CompletedTask;
    }
}
