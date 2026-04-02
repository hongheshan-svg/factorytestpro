using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UTF.Core;
using UTF.Plugin.Abstractions;

namespace UTF.Plugin.Host;

/// <summary>
/// 插件服务适配器 - 将 StepExecutorPluginHost 适配为 IPluginService
/// </summary>
public class PluginServiceAdapter : IPluginService
{
    private readonly StepExecutorPluginHost _pluginHost;

    public PluginServiceAdapter(StepExecutorPluginHost pluginHost)
    {
        _pluginHost = pluginHost;
    }

    public async Task<bool> InitializeAsync(CancellationToken ct = default)
    {
        await _pluginHost.InitializeAsync(ct);
        return true;
    }

    public bool CanHandle(string stepType, string channel)
    {
        return _pluginHost.LoadedPlugins.Any(p =>
            p.SupportedStepTypes.Contains(stepType) &&
            p.SupportedChannels.Contains(channel));
    }

    public async Task<StepExecutionResult> ExecuteAsync(StepExecutionRequest request, CancellationToken ct)
    {
        return await _pluginHost.ExecuteAsync(request, ct);
    }

    public IReadOnlyList<PluginMetadata> LoadedPlugins => _pluginHost.LoadedPlugins;
}
