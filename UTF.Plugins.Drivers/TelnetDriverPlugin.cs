using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UTF.Plugin.Abstractions;

namespace UTF.Plugins.Drivers;

/// <summary>
/// Telnet 通信驱动插件 - 通过 Telnet 协议与 DUT 进行网络通信
/// </summary>
public sealed class TelnetDriverPlugin : DeviceDriverPluginBase
{
    private TcpClient? _client;
    private NetworkStream? _stream;
    private int _port = 23;
    private int _readTimeoutMs = 3000;
    private string _lineEnding = "\r\n";
    private string _promptPattern = ">";
    private Encoding _encoding = Encoding.UTF8;

    public override PluginMetadata Metadata { get; } = new()
    {
        PluginId = "utf.driver.telnet",
        Name = "UTF Telnet Driver",
        Version = "1.0.0",
        PluginApiVersion = PluginApiVersions.V1,
        SupportedStepTypes = new[] { "network", "telnet", "tcp" },
        SupportedChannels = new[] { "network", "telnet", "tcp" },
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

        if (context.Settings.TryGetValue("PromptPattern", out var pp) && !string.IsNullOrEmpty(pp))
        {
            _promptPattern = pp;
        }

        if (context.Settings.TryGetValue("Encoding", out var enc))
        {
            _encoding = enc.ToLowerInvariant() switch
            {
                "ascii" => Encoding.ASCII,
                "utf8" or "utf-8" => Encoding.UTF8,
                "gbk" or "gb2312" => Encoding.GetEncoding("GBK"),
                _ => Encoding.UTF8
            };
        }
    }

    public override bool CanHandle(string stepType, string channel)
    {
        var normalizedType = (stepType ?? string.Empty).Trim().ToLowerInvariant();
        var normalizedChannel = (channel ?? string.Empty).Trim().ToLowerInvariant();
        return normalizedType is "network" or "telnet" or "tcp"
            || normalizedChannel is "network" or "telnet" or "tcp";
    }

    protected override string ResolveEndpoint(StepExecutionRequest request)
    {
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

            // 读取并丢弃初始 Telnet 协商字节和欢迎信息
            await DrainInitialDataAsync(ct);

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
            throw new InvalidOperationException("Telnet 连接未建立");
        }

        var commandBytes = _encoding.GetBytes(command + _lineEnding);
        await _stream.WriteAsync(commandBytes, ct);
        await _stream.FlushAsync(ct);

        return await ReadResponseAsync(ct);
    }

    protected override Task DisconnectCoreAsync(CancellationToken ct)
    {
        CleanupConnection();
        return Task.CompletedTask;
    }

    private async Task<string> ReadResponseAsync(CancellationToken ct)
    {
        var buffer = new byte[4096];
        var response = new StringBuilder();
        var noDataCount = 0;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (_stream!.DataAvailable)
                {
                    var bytesRead = await _stream.ReadAsync(buffer, ct);
                    if (bytesRead > 0)
                    {
                        var chunk = FilterTelnetNegotiation(buffer, bytesRead);
                        response.Append(_encoding.GetString(chunk));
                        noDataCount = 0;

                        // 检查是否到达提示符
                        if (response.ToString().TrimEnd().EndsWith(_promptPattern, StringComparison.OrdinalIgnoreCase))
                        {
                            break;
                        }
                    }
                }
                else
                {
                    await Task.Delay(100, ct);
                    noDataCount++;
                    if (noDataCount >= 30 && response.Length > 0)
                    {
                        break;
                    }
                }
            }
            catch (IOException)
            {
                break;
            }
        }

        return response.ToString().Trim();
    }

    private async Task DrainInitialDataAsync(CancellationToken ct)
    {
        var buffer = new byte[4096];
        try
        {
            await Task.Delay(500, ct);
            while (_stream!.DataAvailable)
            {
                _ = await _stream.ReadAsync(buffer, 0, buffer.Length, ct);
            }
        }
        catch
        {
            // 忽略初始读取错误
        }
    }

    /// <summary>
    /// 过滤 Telnet IAC 协商序列 (0xFF ...)
    /// </summary>
    private static byte[] FilterTelnetNegotiation(byte[] data, int length)
    {
        var filtered = new MemoryStream();
        int i = 0;
        while (i < length)
        {
            if (data[i] == 0xFF && i + 2 < length)
            {
                // IAC 序列: FF XX XX — 跳过 3 字节
                i += 3;
            }
            else
            {
                filtered.WriteByte(data[i]);
                i++;
            }
        }

        return filtered.ToArray();
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
