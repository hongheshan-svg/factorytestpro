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
        // AND 语义：stepType 与 channel 必须同时匹配。
        // 历史上支持 network/telnet/tcp 多通道，全部声明在集合中即可保持匹配。
        var supportedTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "network", "telnet", "tcp"
        };
        var supportedChannels = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "network", "telnet", "tcp"
        };
        return DefaultCanHandle(stepType ?? string.Empty, channel ?? string.Empty, supportedTypes, supportedChannels);
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
            await _client.ConnectAsync(host, port, ct).ConfigureAwait(false);

            _stream = _client.GetStream();
            _stream.ReadTimeout = _readTimeoutMs;
            _stream.WriteTimeout = _readTimeoutMs;

            // 读取并丢弃初始 Telnet 协商字节和欢迎信息
            await DrainInitialDataAsync(ct).ConfigureAwait(false);

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
        await _stream.WriteAsync(commandBytes, ct).ConfigureAwait(false);
        await _stream.FlushAsync(ct).ConfigureAwait(false);

        return await ReadResponseAsync(ct).ConfigureAwait(false);
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

        // 用链接取消令牌施加读取超时上限，避免 DataAvailable 轮询 + Task.Delay 的忙等。
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        linkedCts.CancelAfter(_readTimeoutMs);

        try
        {
            while (!linkedCts.IsCancellationRequested)
            {
                int bytesRead;
                try
                {
                    bytesRead = await _stream!.ReadAsync(buffer, linkedCts.Token).ConfigureAwait(false);
                }
                catch (IOException)
                {
                    break;
                }

                if (bytesRead > 0)
                {
                    var chunk = FilterTelnetNegotiation(buffer, bytesRead);
                    response.Append(_encoding.GetString(chunk));

                    // 检查是否到达提示符
                    if (response.ToString().TrimEnd().EndsWith(_promptPattern, StringComparison.OrdinalIgnoreCase))
                    {
                        break;
                    }
                }
                else
                {
                    // 对端关闭
                    break;
                }
            }
        }
        catch (OperationCanceledException) when (linkedCts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            // 读取超时：返回已收到的内容（若有），与历史行为一致
        }

        return response.ToString().Trim();
    }

    private async Task DrainInitialDataAsync(CancellationToken ct)
    {
        var buffer = new byte[4096];
        try
        {
            await Task.Delay(500, ct).ConfigureAwait(false);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            linkedCts.CancelAfter(_readTimeoutMs);
            while (_stream!.DataAvailable)
            {
                _ = await _stream.ReadAsync(buffer.AsMemory(0, buffer.Length), linkedCts.Token).ConfigureAwait(false);
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
            // IAC 序列: FF XX XX — 跳过 3 字节。注意边界：i+2 <= length 才是一个完整的三字节序列。
            // TODO(RFC854): 当前仅跳过固定 3 字节；应进一步处理 SB...SE 子协商可变长度。
            if (data[i] == 0xFF && i + 2 <= length)
            {
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
