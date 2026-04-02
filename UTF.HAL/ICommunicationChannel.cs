using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace UTF.HAL;

/// <summary>
/// 通信通道接口 - 支持任意通信协议的可插拔抽象
/// </summary>
public interface ICommunicationChannel : IDisposable
{
    /// <summary>通道名称（如 Serial, Network, CAN, I2C）</summary>
    string ChannelName { get; }

    /// <summary>是否已连接</summary>
    bool IsConnected { get; }

    /// <summary>连接通道</summary>
    Task<bool> ConnectAsync(Dictionary<string, object> parameters, CancellationToken ct = default);

    /// <summary>断开通道</summary>
    Task<bool> DisconnectAsync(CancellationToken ct = default);

    /// <summary>发送命令并接收响应</summary>
    Task<CommunicationResult> SendCommandAsync(string command, int timeoutMs = 5000, CancellationToken ct = default);

    /// <summary>读取数据</summary>
    Task<CommunicationResult> ReadAsync(int length = -1, int timeoutMs = 5000, CancellationToken ct = default);

    /// <summary>写入数据</summary>
    Task<CommunicationResult> WriteAsync(byte[] data, int timeoutMs = 5000, CancellationToken ct = default);
}

/// <summary>
/// 通信结果
/// </summary>
public sealed record CommunicationResult
{
    public bool Success { get; init; }
    public string Data { get; init; } = string.Empty;
    public byte[] RawData { get; init; } = Array.Empty<byte>();
    public string ErrorMessage { get; init; } = string.Empty;
    public TimeSpan Duration { get; init; }
    public Dictionary<string, object> Metadata { get; init; } = new();
}
