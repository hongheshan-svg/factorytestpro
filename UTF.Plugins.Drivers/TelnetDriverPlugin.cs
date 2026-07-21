using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UTF.Plugin.Abstractions;

namespace UTF.Plugins.Drivers;

/// <summary>
/// Telnet 通信驱动插件 - 通过 Telnet 协议与 DUT 进行网络通信。
/// 每个 host:port 端点独立连接，支持多 DUT 并行。
/// </summary>
public sealed class TelnetDriverPlugin : DeviceDriverPluginBase
{
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

    protected override async Task<object?> CreateConnectionAsync(string endpoint, CancellationToken ct)
    {
        try
        {
            var parts = endpoint.Split(':', 2);
            var host = parts[0];
            var port = parts.Length > 1 && int.TryParse(parts[1], out var p) ? p : _port;

            var client = new TcpClient();
            await client.ConnectAsync(host, port, ct).ConfigureAwait(false);

            var stream = client.GetStream();
            stream.ReadTimeout = _readTimeoutMs;
            stream.WriteTimeout = _readTimeoutMs;

            var state = new TelnetConnection(client, stream, _encoding, _readTimeoutMs, _promptPattern, _lineEnding);
            await state.DrainInitialDataAsync(ct).ConfigureAwait(false);
            return state;
        }
        catch
        {
            return null;
        }
    }

    protected override Task<string> SendCommandOnConnectionAsync(object connection, string command, CancellationToken ct)
    {
        var state = (TelnetConnection)connection;
        return state.SendCommandAsync(command, ct);
    }

    protected override Task CloseConnectionAsync(object connection, CancellationToken ct)
    {
        if (connection is TelnetConnection state)
        {
            state.Dispose();
        }

        return Task.CompletedTask;
    }

    private sealed class TelnetConnection : IDisposable
    {
        private readonly TcpClient _client;
        private readonly NetworkStream _stream;
        private readonly Encoding _encoding;
        private readonly int _readTimeoutMs;
        private readonly string _promptPattern;
        private readonly string _lineEnding;

        public TelnetConnection(
            TcpClient client,
            NetworkStream stream,
            Encoding encoding,
            int readTimeoutMs,
            string promptPattern,
            string lineEnding)
        {
            _client = client;
            _stream = stream;
            _encoding = encoding;
            _readTimeoutMs = readTimeoutMs;
            _promptPattern = promptPattern;
            _lineEnding = lineEnding;
        }

        public async Task DrainInitialDataAsync(CancellationToken ct)
        {
            var buffer = new byte[4096];
            try
            {
                await Task.Delay(500, ct).ConfigureAwait(false);
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                linkedCts.CancelAfter(_readTimeoutMs);
                while (_stream.DataAvailable)
                {
                    _ = await _stream.ReadAsync(buffer.AsMemory(0, buffer.Length), linkedCts.Token).ConfigureAwait(false);
                }
            }
            catch
            {
                // 忽略初始读取错误
            }
        }

        public async Task<string> SendCommandAsync(string command, CancellationToken ct)
        {
            if (!_client.Connected)
            {
                throw new InvalidOperationException("Telnet 连接未建立");
            }

            var commandBytes = _encoding.GetBytes(command + _lineEnding);
            await _stream.WriteAsync(commandBytes, ct).ConfigureAwait(false);
            await _stream.FlushAsync(ct).ConfigureAwait(false);

            return await ReadResponseAsync(ct).ConfigureAwait(false);
        }

        private async Task<string> ReadResponseAsync(CancellationToken ct)
        {
            var buffer = new byte[4096];
            var response = new StringBuilder();

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            linkedCts.CancelAfter(_readTimeoutMs);

            try
            {
                while (!linkedCts.IsCancellationRequested)
                {
                    int bytesRead;
                    try
                    {
                        bytesRead = await _stream.ReadAsync(buffer, linkedCts.Token).ConfigureAwait(false);
                    }
                    catch (IOException)
                    {
                        break;
                    }

                    if (bytesRead > 0)
                    {
                        var chunk = FilterTelnetNegotiation(buffer, bytesRead);
                        response.Append(_encoding.GetString(chunk));

                        if (response.ToString().TrimEnd().EndsWith(_promptPattern, StringComparison.OrdinalIgnoreCase))
                        {
                            break;
                        }
                    }
                    else
                    {
                        break;
                    }
                }
            }
            catch (OperationCanceledException) when (linkedCts.IsCancellationRequested && !ct.IsCancellationRequested)
            {
                // 读取超时：返回已收到的内容
            }

            return response.ToString().Trim();
        }

        private static byte[] FilterTelnetNegotiation(byte[] data, int length)
        {
            var filtered = new MemoryStream();
            int i = 0;
            while (i < length)
            {
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

        public void Dispose()
        {
            _stream.Dispose();
            _client.Dispose();
        }
    }
}
