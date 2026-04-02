using System;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UTF.Plugin.Abstractions;

namespace UTF.Plugins.Drivers;

/// <summary>
/// 串口通信驱动插件 - 通过 RS232/RS485 串口与 DUT 通信
/// </summary>
public sealed class SerialDriverPlugin : DeviceDriverPluginBase
{
    private SerialPort? _serialPort;
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
        var normalizedType = (stepType ?? string.Empty).Trim().ToLowerInvariant();
        var normalizedChannel = (channel ?? string.Empty).Trim().ToLowerInvariant();
        return normalizedType is "serial" or "uart" or "rs232" or "rs485"
            || normalizedChannel is "serial" or "uart" or "com";
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

    protected override Task<bool> ConnectCoreAsync(string endpoint, CancellationToken ct)
    {
        try
        {
            _serialPort = new SerialPort(endpoint, _baudRate, _parity, _dataBits, _stopBits)
            {
                ReadTimeout = _readTimeoutMs,
                WriteTimeout = _readTimeoutMs,
                Encoding = Encoding.UTF8
            };
            _serialPort.Open();
            _serialPort.DiscardInBuffer();
            _serialPort.DiscardOutBuffer();
            return Task.FromResult(true);
        }
        catch
        {
            _serialPort?.Dispose();
            _serialPort = null;
            return Task.FromResult(false);
        }
    }

    protected override async Task<string> SendCommandCoreAsync(string command, CancellationToken ct)
    {
        if (_serialPort == null || !_serialPort.IsOpen)
        {
            throw new InvalidOperationException("串口未打开");
        }

        _serialPort.DiscardInBuffer();
        _serialPort.Write(command + _lineEnding);

        // 等待响应
        var buffer = new StringBuilder();
        var noDataCount = 0;
        var maxNoDataCycles = 20;

        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(100, ct);

            if (_serialPort.BytesToRead > 0)
            {
                buffer.Append(_serialPort.ReadExisting());
                noDataCount = 0;
            }
            else
            {
                noDataCount++;
                if (noDataCount >= maxNoDataCycles || buffer.Length > 0)
                {
                    break;
                }
            }
        }

        return buffer.ToString().Trim();
    }

    protected override Task DisconnectCoreAsync(CancellationToken ct)
    {
        if (_serialPort != null)
        {
            if (_serialPort.IsOpen)
            {
                _serialPort.Close();
            }

            _serialPort.Dispose();
            _serialPort = null;
        }

        return Task.CompletedTask;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _serialPort?.Dispose();
            _serialPort = null;
        }

        base.Dispose(disposing);
    }
}
