using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UTF.Configuration.Models;

namespace UTF.Configuration;

/// <summary>
/// 配置适配器 - 提供统一配置读取与校验方法。
/// </summary>
public class UnifiedConfigurationAdapter : IUnifiedConfigurationAdapter
{
    public string GetProductModel(UnifiedConfiguration config)
    {
        return config?.DUTConfiguration?.ProductInfo?.Model ?? "Generic";
    }

    public List<UnifiedTestStepConfig> GetTestSteps(UnifiedConfiguration config)
    {
        return config?.TestProjectConfiguration?.TestProject?.Steps
            ?? new List<UnifiedTestStepConfig>();
    }

    public int GetMaxConcurrent(UnifiedConfiguration config)
    {
        return config?.DUTConfiguration?.GlobalSettings?.DefaultMaxConcurrent ?? 16;
    }

    public List<string> GetSerialPorts(UnifiedConfiguration config)
    {
        var ports = EndpointMapper.GetSerialAddresses(config);
        if (ports.Count > 0)
        {
            return ports;
        }

        // Default only when neither Endpoints nor legacy lists provide values.
        return new List<string> { "COM3", "COM4", "COM5", "COM6" };
    }

    public List<string> GetNetworkHosts(UnifiedConfiguration config)
    {
        var hosts = EndpointMapper.GetNetworkAddresses(config);
        if (hosts.Count > 0)
        {
            return hosts;
        }

        return new List<string> { "192.168.1.10", "192.168.1.11" };
    }

    /// <summary>
    /// Returns normalized endpoints (synthesizes from legacy lists when empty).
    /// </summary>
    public List<EndpointDefinition> GetEndpoints(UnifiedConfiguration config)
    {
        return EndpointMapper.NormalizeEndpoints(config);
    }

    public string GetNamingTemplate(UnifiedConfiguration config)
    {
        return config?.DUTConfiguration?.NamingConfig?.Template
            ?? "{TypeName}测试工位{Index}";
    }

    public string GetIdTemplate(UnifiedConfiguration config)
    {
        return config?.DUTConfiguration?.NamingConfig?.IdTemplate
            ?? "DUT-{Index}";
    }

    public bool ValidateConfiguration(UnifiedConfiguration config)
    {
        return ValidateConfigurationWithErrors(config).Count == 0;
    }

    public List<string> ValidateConfigurationWithErrors(UnifiedConfiguration config)
    {
        var errors = new List<string>();
        if (config == null)
        {
            errors.Add("配置对象为空");
            return errors;
        }

        if (string.IsNullOrEmpty(config.ConfigurationInfo?.Name))
        {
            errors.Add("配置名称不能为空");
        }

        if (config.SystemSettings == null)
        {
            errors.Add("系统设置缺失");
        }

        if (config.DUTConfiguration == null)
        {
            errors.Add("DUT配置缺失");
        }

        if (config.TestProjectConfiguration == null)
        {
            errors.Add("测试项目配置缺失");
        }

        var maxConcurrent = config.DUTConfiguration?.GlobalSettings?.DefaultMaxConcurrent ?? 0;
        if (maxConcurrent is < 1 or > 256)
        {
            errors.Add("DefaultMaxConcurrent 必须在 1 到 256 之间");
        }

        // Ensure Endpoints are synthesized from legacy lists before validation.
        EndpointMapper.NormalizeEndpoints(config);
        errors.AddRange(EndpointMapper.ValidateEndpoints(config.DUTConfiguration?.Endpoints));

        if (string.IsNullOrWhiteSpace(config.TestProjectConfiguration?.TestProject?.Id))
        {
            errors.Add("测试项目 ID 不能为空");
        }

        var steps = GetTestSteps(config);
        if (steps.Count == 0)
        {
            errors.Add("测试步骤列表为空");
            return errors;
        }

        var ids = steps.Select(s => s.Id).Where(id => !string.IsNullOrWhiteSpace(id)).ToList();
        if (ids.Count != steps.Count)
        {
            errors.Add("每个步骤都必须具有非空 ID");
        }

        var duplicateIds = ids
            .GroupBy(id => id, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        if (duplicateIds.Count > 0)
        {
            errors.Add($"步骤ID重复: {string.Join(", ", duplicateIds)}");
        }

        for (var i = 0; i < steps.Count; i++)
        {
            var s = steps[i];
            var label = $"步骤{i + 1}({s.Id})";
            if (string.IsNullOrEmpty(s.Name))
            {
                errors.Add($"{label}: 名称不能为空");
            }

            if (s.Enabled && string.IsNullOrWhiteSpace(s.Type))
            {
                errors.Add($"{label}: 类型不能为空");
            }

            if (s.Enabled && string.IsNullOrWhiteSpace(s.Channel))
            {
                errors.Add($"{label}: 通道不能为空");
            }

            if (s.Enabled && string.IsNullOrWhiteSpace(s.Command))
            {
                errors.Add($"{label}: 命令不能为空");
            }

            if ((s.Timeout ?? 0) <= 0)
            {
                errors.Add($"{label}: 超时必须大于 0");
            }

            if ((s.Delay ?? 0) < 0)
            {
                errors.Add($"{label}: 延迟不能为负数");
            }

            if ((s.RetryCount ?? 0) is < 0 or > 10)
            {
                errors.Add($"{label}: 重试次数必须在 0 到 10 之间");
            }

            if (!string.IsNullOrWhiteSpace(s.StoreResultAs) &&
                !Regex.IsMatch(s.StoreResultAs, @"^[A-Za-z_][A-Za-z0-9_.:-]*$", RegexOptions.CultureInvariant))
            {
                errors.Add($"{label}: StoreResultAs 不是有效的上下文键");
            }

            if (s.Expected?.StartsWith("regex:", StringComparison.OrdinalIgnoreCase) == true)
            {
                try
                {
                    _ = new Regex(s.Expected["regex:".Length..], RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2));
                }
                catch (ArgumentException)
                {
                    errors.Add($"{label}: Expected 正则表达式无效");
                }
            }
        }

        return errors;
    }

    public string GetConfigurationSummary(UnifiedConfiguration config)
    {
        if (config == null)
        {
            return "无效配置";
        }

        var productModel = GetProductModel(config);
        var maxConcurrent = GetMaxConcurrent(config);
        var stepCount = GetTestSteps(config).Count;
        return $"产品: {productModel} | 并发数: {maxConcurrent} | 测试步骤: {stepCount}个";
    }
}
