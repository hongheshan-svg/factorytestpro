using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UTF.Plugin.Abstractions;

namespace UTF.Core;

/// <summary>
/// 插件服务接口 - 统一插件管理和执行
/// </summary>
public interface IPluginService
{
    Task<bool> InitializeAsync(CancellationToken ct = default);
    bool CanHandle(string stepType, string channel);
    Task<StepExecutionResult> ExecuteAsync(StepExecutionRequest request, CancellationToken ct);
    IReadOnlyList<PluginMetadata> LoadedPlugins { get; }
}
