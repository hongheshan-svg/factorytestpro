using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UTF.Plugin.Abstractions;

namespace UTF.Plugins.Drivers;

/// <summary>
/// SCPI 仪器通信驱动插件 - 通过 TCP/GPIB 与示波器、万用表、电源等仪器通信
/// 支持标准 SCPI 命令（*IDN?, *RST, MEASure 等）
/// </summary>
public sealed class ScpiDriverPlugin : DeviceDriverPluginBase
{
    private TcpClient? _client;
    private NetworkStream? _stream;
    private int _port = 5025;
    private int _readTimeoutMs = 5000;
    private string _lineEnding = "\n";
    private Encoding _encoding = Encoding.ASCII;

    public override PluginMetadata Metadata { get; } = new()
    {
        PluginId = "utf.driver.scpi",
        Name = "UTF SCPI Instrument Driver",
        Version = "1.0.0",
        PluginApiVersion = PluginApiVersions.V1,
        SupportedStepTypes = new[] { "instrument", "scpi", "gpib", "measure" },
        SupportedChannels = new[] { "scpi", "instrument", "gpib", "lxi" },
        Priority = 10
    };

    protected override void OnInitialize(PluginInitContext context)
    {
        if (context.Settings.TryGetValue("Port", out var p) && int.TryParse(p, out var port))
        {
            _port = port;
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
        return normalizedType is "instrument" or "scpi" or "gpib" or "measure"
            || normalizedChannel is "scpi" or "instrument" or "gpib" or "lxi";
    }

    protected override string ResolveEndpoint(StepExecutionRequest request)
    {
        // SCPI 仪器通常通过 Host:Port 连接 (LXI)
        if (request.Parameters.TryGetValue("InstrumentAddress", out var addr) && addr != null)
        {
            return addr.ToString()!;
        }

        if (request.Parameters.TryGetValue("Host", out var host) && host != null)
        {
            var portStr = request.Parameters.TryGetValue("Port", out var p) ? p?.ToString() : null;
            return portStr != null ? $"{host}:{portStr}" : $"{host}:{_port}";
        }

        return base.ResolveEndpoint(request);
    }

    protected override async Task<bool> ConnectCoreAsync(string endpoint, CancellationToken ct)
    {
        try
        {
            var parts = endpoint.Split(':', 2);
            var host = parts[0];
            var port = parts.Length > 1 && int.TryParse(parts[1], out var p) ? p : _port;

            _client = new TcpClient();
            await _client.ConnectAsync(host, port, ct);

            _stream = _client.GetStream();
            _stream.ReadTimeout = _readTimeoutMs;
            _stream.WriteTimeout = _readTimeoutMs;

            return true;
        }
        catch
        {
            CleanupConnection();
            return false;
        }
    }

    protected override async Task<string> SendCommandCoreAsync(string command, CancellationToken ct)
    {
        if (_stream == null || !(_client?.Connected ?? false))
        {
            throw new InvalidOperationException("SCPI 连接未建立");
        }

        var commandBytes = _encoding.GetBytes(command + _lineEnding);
        await _stream.WriteAsync(commandBytes, ct);
        await _stream.FlushAsync(ct);

        // SCPI 查询命令以 ? 结尾，需要读取响应；设置命令无响应
        if (command.TrimEnd().EndsWith('?'))
        {
            return await ReadScpiResponseAsync(ct);
        }

        // 对于设置命令，发送后短暂等待确保仪器处理
        await Task.Delay(50, ct);
        return "OK";
    }

    protected override string PostProcessOutput(string output, StepExecutionRequest request)
    {
        // SCPI 响应通常以 \n 结尾，去除多余空白
        return output.Trim();
    }

    protected override Task DisconnectCoreAsync(CancellationToken ct)
    {
        CleanupConnection();
        return Task.CompletedTask;
    }

    private async Task<string> ReadScpiResponseAsync(CancellationToken ct)
    {
        var buffer = new byte[4096];
        var response = new StringBuilder();

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var bytesRead = await _stream!.ReadAsync(buffer, ct);
                if (bytesRead > 0)
                {
                    var chunk = _encoding.GetString(buffer, 0, bytesRead);
                    response.Append(chunk);

                    // SCPI 响应以 \n 结尾
                    if (chunk.EndsWith('\n'))
                    {
                        break;
                    }
                }
                else
                {
                    break;
                }
            }
            catch (IOException)
            {
                break;
            }
        }

        return response.ToString().Trim();
    }

    private void CleanupConnection()
    {
        _stream?.Dispose();
        _stream = null;
        _client?.Dispose();
        _client = null;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            CleanupConnection();
        }

        base.Dispose(disposing);
    }
}
