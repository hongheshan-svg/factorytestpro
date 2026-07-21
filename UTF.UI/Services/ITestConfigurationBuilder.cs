using UTF.UI.Models;

namespace UTF.UI.Services;

/// <summary>
/// 测试配置构建器接口：将快速向导输入转换为统一配置对象。
/// </summary>
public interface ITestConfigurationBuilder
{
    /// <summary>
    /// 基于向导输入构建一份完整的 <see cref="UnifiedConfiguration"/>。
    /// </summary>
    /// <param name="input">向导输入。</param>
    /// <returns>组装好的统一配置。</returns>
    UnifiedConfiguration Build(QuickTestWizardInput input);
}
