using System;
using System.Threading.Tasks;

namespace UTF.Core;

/// <summary>
/// 配置服务接口 — 统一配置访问入口
/// </summary>
public interface IConfigurationService
{
    Task<T?> GetConfigurationSectionAsync<T>(string section) where T : class;
    Task SaveConfigurationAsync(object config);
    Task RefreshAsync();
    event EventHandler? ConfigurationChanged;
}
