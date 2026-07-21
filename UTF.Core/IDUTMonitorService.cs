using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UTF.Plugin.Abstractions;

namespace UTF.Core;

/// <summary>
/// DUT 监控服务接口
/// </summary>
public interface IDUTMonitorService
{
    Task InitializeAsync(int dutCount);
    Task StartAllTestsAsync(CancellationToken ct = default);
    Task StopAllTestsAsync();
    event Action? StatisticsUpdateRequested;
    event Action? AllTestsCompleted;

    /// <summary>获取已加载插件的元数据列表</summary>
    IReadOnlyList<PluginMetadata> GetLoadedPlugins();
}
