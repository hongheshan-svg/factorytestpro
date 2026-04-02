using System;
using System.Collections.Generic;
using System.Linq;

namespace UTF.UI.Services
{
    /// <summary>
    /// 配置适配器 - 提供统一配置读取方法
    /// </summary>
    public class ConfigurationAdapter : IConfigurationAdapter
    {
        /// <summary>
        /// 获取产品型号
        /// </summary>
        public string GetProductModel(UnifiedConfiguration config)
        {
            return config?.DUTConfiguration?.ProductInfo?.Model ?? "Generic";
        }

        /// <summary>
        /// 获取测试步骤列表
        /// </summary>
        public List<TestStepConfig> GetTestSteps(UnifiedConfiguration config)
        {
            return config?.TestProjectConfiguration?.TestProject?.Steps
                ?? new List<TestStepConfig>();
        }

        /// <summary>
        /// 获取最大并发DUT数量
        /// </summary>
        public int GetMaxConcurrent(UnifiedConfiguration config)
        {
            return config?.DUTConfiguration?.GlobalSettings?.DefaultMaxConcurrent ?? 16;
        }

        /// <summary>
        /// 获取串口列表
        /// </summary>
        public List<string> GetSerialPorts(UnifiedConfiguration config)
        {
            return config?.DUTConfiguration?.CommunicationEndpoints?.SerialPorts
                ?? new List<string> { "COM3", "COM4", "COM5", "COM6" };
        }

        /// <summary>
        /// 获取网络主机列表
        /// </summary>
        public List<string> GetNetworkHosts(UnifiedConfiguration config)
        {
            return config?.DUTConfiguration?.CommunicationEndpoints?.NetworkHosts
                ?? new List<string> { "192.168.1.10", "192.168.1.11" };
        }

        /// <summary>
        /// 获取命名模板
        /// </summary>
        public string GetNamingTemplate(UnifiedConfiguration config)
        {
            return config?.DUTConfiguration?.NamingConfig?.Template
                ?? "{TypeName}测试工位{Index}";
        }

        /// <summary>
        /// 获取ID模板
        /// </summary>
        public string GetIdTemplate(UnifiedConfiguration config)
        {
            return config?.DUTConfiguration?.NamingConfig?.IdTemplate
                ?? "DUT-{Index}";
        }

        /// <summary>
        /// 验证配置完整性
        /// </summary>
        public bool ValidateConfiguration(UnifiedConfiguration config)
        {
            if (config == null) return false;
            if (string.IsNullOrEmpty(config.ConfigurationInfo?.Name)) return false;
            if (config.SystemSettings == null) return false;
            if (config.DUTConfiguration == null) return false;
            if (config.TestProjectConfiguration == null) return false;

            var steps = GetTestSteps(config);
            if (steps == null || !steps.Any()) return false;

            // 检查步骤ID唯一性
            var ids = steps.Select(s => s.Id).Where(id => !string.IsNullOrEmpty(id)).ToList();
            if (ids.Count != ids.Distinct().Count()) return false;

            // 检查每个步骤基本字段
            return steps.All(s => !string.IsNullOrEmpty(s.Name) && (s.Timeout ?? 0) >= 0);
        }

        /// <summary>
        /// 验证配置并返回错误列表
        /// </summary>
        public List<string> ValidateConfigurationWithErrors(UnifiedConfiguration config)
        {
            var errors = new List<string>();
            if (config == null) { errors.Add("配置对象为空"); return errors; }
            if (string.IsNullOrEmpty(config.ConfigurationInfo?.Name)) errors.Add("配置名称不能为空");
            if (config.SystemSettings == null) errors.Add("系统设置缺失");
            if (config.DUTConfiguration == null) errors.Add("DUT配置缺失");
            if (config.TestProjectConfiguration == null) errors.Add("测试项目配置缺失");

            var steps = GetTestSteps(config);
            if (steps == null || !steps.Any()) { errors.Add("测试步骤列表为空"); return errors; }

            // 检查步骤ID唯一性
            var ids = steps.Select(s => s.Id).Where(id => !string.IsNullOrEmpty(id)).ToList();
            var duplicateIds = ids.GroupBy(id => id).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            if (duplicateIds.Any()) errors.Add($"步骤ID重复: {string.Join(", ", duplicateIds)}");

            // 检查每个步骤字段
            var validPrefixes = new[] { "contains:", "equals:", "regex:" };
            for (int i = 0; i < steps.Count; i++)
            {
                var s = steps[i];
                var label = $"步骤{i + 1}({s.Id})";
                if (string.IsNullOrEmpty(s.Name)) errors.Add($"{label}: 名称不能为空");
                if ((s.Timeout ?? 0) < 0) errors.Add($"{label}: 超时不能为负数");
                if (!string.IsNullOrEmpty(s.Expected))
                {
                    bool validPrefix = validPrefixes.Any(p => s.Expected.StartsWith(p));
                    if (!validPrefix) errors.Add($"{label}: Expected前缀无效，应为 contains:/equals:/regex: 或留空");
                }
            }

            return errors;
        }

        /// <summary>
        /// 获取配置摘要信息
        /// </summary>
        public string GetConfigurationSummary(UnifiedConfiguration config)
        {
            if (config == null) return "无效配置";
            var productModel = GetProductModel(config);
            var maxConcurrent = GetMaxConcurrent(config);
            var steps = GetTestSteps(config);
            var stepCount = steps?.Count ?? 0;
            return $"产品: {productModel} | 并发数: {maxConcurrent} | 测试步骤: {stepCount}个";
        }
    }
}
