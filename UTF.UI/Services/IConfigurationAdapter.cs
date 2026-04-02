using System.Collections.Generic;

namespace UTF.UI.Services;

public interface IConfigurationAdapter
{
    string GetProductModel(UnifiedConfiguration config);
    List<TestStepConfig> GetTestSteps(UnifiedConfiguration config);
    int GetMaxConcurrent(UnifiedConfiguration config);
    List<string> GetSerialPorts(UnifiedConfiguration config);
    List<string> GetNetworkHosts(UnifiedConfiguration config);
    string GetNamingTemplate(UnifiedConfiguration config);
    string GetIdTemplate(UnifiedConfiguration config);
    bool ValidateConfiguration(UnifiedConfiguration config);
    string GetConfigurationSummary(UnifiedConfiguration config);
}
