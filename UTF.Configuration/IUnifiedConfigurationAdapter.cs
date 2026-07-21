using System.Collections.Generic;
using UTF.Configuration.Models;

namespace UTF.Configuration;

/// <summary>
/// 统一配置读取与校验适配器（无 UI 依赖）。
/// </summary>
public interface IUnifiedConfigurationAdapter
{
    string GetProductModel(UnifiedConfiguration config);
    List<UnifiedTestStepConfig> GetTestSteps(UnifiedConfiguration config);
    int GetMaxConcurrent(UnifiedConfiguration config);
    List<string> GetSerialPorts(UnifiedConfiguration config);
    List<string> GetNetworkHosts(UnifiedConfiguration config);
    /// <summary>Normalized endpoint list (synthesized from legacy when empty).</summary>
    List<EndpointDefinition> GetEndpoints(UnifiedConfiguration config);
    string GetNamingTemplate(UnifiedConfiguration config);
    string GetIdTemplate(UnifiedConfiguration config);
    bool ValidateConfiguration(UnifiedConfiguration config);
    List<string> ValidateConfigurationWithErrors(UnifiedConfiguration config);
    string GetConfigurationSummary(UnifiedConfiguration config);
}
