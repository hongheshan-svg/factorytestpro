using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UTF.Plugin.Abstractions;

namespace UTF.Plugins.Drivers;

/// <summary>
/// SCPI 仪器通信驱动插件 - 通过 TCP/GPIB 与示波器、万用表、电源等仪器通信。
/// 每个仪器地址独立连接，支持多仪器并行。
/// </summary>
public sealed class ScpiDriverPlugin : DeviceDriverPluginBase
{
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
        var supportedTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "instrument", "scpi", "gpib", "measure"
        };
        var supportedChannels = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "scpi", "instrument", "gpib", "lxi"
        };
        return DefaultCanHandle(stepType ?? string.Empty, channel ?? string.Empty, supportedTypes, supportedChannels);
    }

    protected override string ResolveEndpoint(StepExecutionRequest request)
    {
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

            return new ScpiConnection(client, stream, _encoding, _readTimeoutMs, _lineEnding);
        }
        catch
        {
            return null;
        }
    }

    protected override Task<string> SendCommandOnConnectionAsync(object connection, string command, CancellationToken ct)
    {
        var state = (ScpiConnection)connection;
        return state.SendCommandAsync(command, ct);
    }

    protected override string PostProcessOutput(string output, StepExecutionRequest request)
    {
        return output.Trim();
    }

    protected override Task CloseConnectionAsync(object connection, CancellationToken ct)
    {
        if (connection is ScpiConnection state)
        {
            state.Dispose();
        }

        return Task.CompletedTask;
    }

    private sealed class ScpiConnection : IDisposable
    {
        private readonly TcpClient _client;
        private readonly NetworkStream _stream;
        private readonly Encoding _encoding;
        private readonly int _readTimeoutMs;
        private readonly string _lineEnding;

        public ScpiConnection(
            TcpClient client,
            NetworkStream stream,
            Encoding encoding,
            int readTimeoutMs,
            string lineEnding)
        {
            _client = client;
            _stream = stream;
            _encoding = encoding;
            _readTimeoutMs = readTimeoutMs;
            _lineEnding = lineEnding;
        }

        public async Task<string> SendCommandAsync(string command, CancellationToken ct)
        {
            if (!_client.Connected)
            {
                throw new InvalidOperationException("SCPI 连接未建立");
            }

            var commandBytes = _encoding.GetBytes(command + _lineEnding);
            await _stream.WriteAsync(commandBytes, ct).ConfigureAwait(false);
            await _stream.FlushAsync(ct).ConfigureAwait(false);

            if (command.TrimEnd().EndsWith('?'))
            {
                return await ReadScpiResponseAsync(ct).ConfigureAwait(false);
            }

            await Task.Delay(50, ct).ConfigureAwait(false);
            return "OK";
        }

        private async Task<string> ReadScpiResponseAsync(CancellationToken ct)
        {
            var buffer = new byte[4096];
            var response = new StringBuilder();

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            linkedCts.CancelAfter(_readTimeoutMs);

            while (!linkedCts.IsCancellationRequested)
            {
                try
                {
                    var bytesRead = await _stream.ReadAsync(buffer.AsMemory(0, buffer.Length), linkedCts.Token)
                        .ConfigureAwait(false);
                    if (bytesRead <= 0)
                    {
                        break;
                    }

                    response.Append(_encoding.GetString(buffer, 0, bytesRead));
                    if (response.ToString().Contains('\n'))
                    {
                        break;
                    }
                }
                catch (OperationCanceledException) when (linkedCts.IsCancellationRequested && !ct.IsCancellationRequested)
                {
                    break;
                }
                catch (IOException)
                {
                    break;
                }
            }

            return response.ToString();
        }

        public void Dispose()
        {
            _stream.Dispose();
            _client.Dispose();
        }
    }
}
