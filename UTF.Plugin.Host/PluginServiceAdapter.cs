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
        var report = await _pluginHost.InitializeAsync(ct).ConfigureAwait(false);
        return report.FailedCount == 0;
    }

    /// <summary>
    /// 判断是否存在可处理指定步骤类型与通道的插件。
    /// 采用 AND 语义：stepType 与 channel 必须同时匹配；任一方声明 "*" 通配符则视为该侧恒匹配。
    /// </summary>
    public bool CanHandle(string stepType, string channel)
    {
        return _pluginHost.LoadedPlugins.Any(p =>
            MatchesSide(p.SupportedStepTypes, stepType) &&
            MatchesSide(p.SupportedChannels, channel));

        static bool MatchesSide(IReadOnlyList<string> supported, string value)
        {
            // 任一方声明 "*" 通配符即视为匹配：插件支持的集合含 "*"，或请求值为 "*"。
            if (supported.Contains("*", StringComparer.OrdinalIgnoreCase) ||
                string.Equals(value, "*", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return supported.Contains(value, StringComparer.OrdinalIgnoreCase);
        }
    }

    public async Task<StepExecutionResult> ExecuteAsync(StepExecutionRequest request, CancellationToken ct)
    {
        return await _pluginHost.ExecuteAsync(request, ct);
    }

    public IReadOnlyList<PluginMetadata> LoadedPlugins => _pluginHost.LoadedPlugins;
}
