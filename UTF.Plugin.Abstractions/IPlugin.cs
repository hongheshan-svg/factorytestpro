using System.Threading;
using System.Threading.Tasks;

namespace UTF.Plugin.Abstractions;

/// <summary>
/// 插件基础接口
/// </summary>
public interface IPlugin
{
    PluginMetadata Metadata { get; }
    Task InitializeAsync(PluginInitContext context, CancellationToken ct = default);
    Task ShutdownAsync(CancellationToken ct = default);
}
