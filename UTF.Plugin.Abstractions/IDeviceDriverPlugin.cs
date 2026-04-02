using System.Threading;
using System.Threading.Tasks;

namespace UTF.Plugin.Abstractions;

/// <summary>
/// 设备驱动插件接口
/// </summary>
public interface IDeviceDriverPlugin : IPlugin
{
    Task<bool> ConnectAsync(string endpoint, CancellationToken ct = default);
    Task<string> SendCommandAsync(string command, CancellationToken ct = default);
    Task DisconnectAsync(CancellationToken ct = default);
}
