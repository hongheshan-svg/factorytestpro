using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using UTF.Logging;

namespace UTF.Core;

/// <summary>
/// 配置驱动测试验证器 - 验证测试配置的正确性和完整性
/// </summary>
public sealed class ConfigDrivenTestValidator
{
    private readonly ILogger _logger;

    public ConfigDrivenTestValidator(ILogger? logger = null)
    {
        _logger = logger ?? LoggerFactory.CreateLogger<ConfigDrivenTestValidator>();
    }

    /// <summary>
    /// 验证测试项目配置
    /// </summary>
    public ValidationReport ValidateTestProject(ConfigTestProject project)
    {
        var report = new ValidationReport
        {
            ProjectId = project.Id,
            ProjectName = project.Name,
            IsValid = true,
            Errors = new List<ValidationError>(),
            Warnings = new List<ValidationWarning>()
        };

        try
        {
            _logger.Info($"验证测试项目: {project.Name}");

            // 验证基本信息
            ValidateBasicInfo(project, report);

            // 验证测试步骤
            ValidateSteps(project, report);

            // 验证步骤顺序
            ValidateStepOrder(project, report);

            // 验证步骤依赖
            ValidateStepDependencies(project, report);

            // 验证超时设置
            ValidateTimeouts(project, report);

            // 验证命令格式
            ValidateCommands(project, report);

            // 验证期望值格式
            ValidateExpectedValues(project, report);

            // 验证变量模板引用
            ValidateVariableTemplates(project, report);

            // 验证条件表达式
            ValidateConditionExpressions(project, report);

            // 验证 StoreResultAs 依赖关系
            ValidateStoreResultDependencies(project, report);

            // 验证 ValidationRules 结构
            ValidateValidationRules(project, report);

            // 验证通道与类型一致性
            ValidateChannelTypeConsistency(project, report);

            report.IsValid = report.Errors.Count == 0;

            _logger.Info($"验证完成: {(report.IsValid ? "通过" : "失败")}, " +
                        $"错误: {report.Errors.Count}, 警告: {report.Warnings.Count}");

            return report;
        }
        catch (Exception ex)
        {
            _logger.Error($"验证测试项目失败: {project.Name}", ex);
            report.IsValid = false;
            report.Errors.Add(new ValidationError
            {
                Code = "VAL_EXCEPTION",
                Message = $"验证过程异常: {ex.Message}",
                Severity = ErrorSeverity.Critical
            });
            return report;
        }
    }

    /// <summary>
    /// 验证基本信息
    /// </summary>
    private void ValidateBasicInfo(ConfigTestProject project, ValidationReport report)
    {
        // 验证 ID
        if (string.IsNullOrWhiteSpace(project.Id))
        {
            report.Errors.Add(new ValidationError
            {
                Code = "VAL_001",
                Message = "测试项目 ID 不能为空",
                Severity = ErrorSeverity.Critical
            });
        }
        else if (!IsValidId(project.Id))
        {
            report.Errors.Add(new ValidationError
            {
                Code = "VAL_002",
                Message = $"测试项目 ID 格式无效: {project.Id}",
                Severity = ErrorSeverity.High,
                Suggestion = "ID 应仅包含字母、数字、下划线和连字符"
            });
        }

        // 验证名称
        if (string.IsNullOrWhiteSpace(project.Name))
        {
            report.Errors.Add(new ValidationError
            {
                Code = "VAL_003",
                Message = "测试项目名称不能为空",
                Severity = ErrorSeverity.High
            });
        }

        // 验证描述
        if (string.IsNullOrWhiteSpace(project.Description))
        {
            report.Warnings.Add(new ValidationWarning
            {
                Code = "VAL_W001",
                Message = "建议添加测试项目描述",
                Suggestion = "添加描述有助于理解测试项目的目的"
            });
        }
    }

    /// <summary>
    /// 验证测试步骤
    /// </summary>
    private void ValidateSteps(ConfigTestProject project, ValidationReport report)
    {
        if (project.Steps == null || !project.Steps.Any())
        {
            report.Errors.Add(new ValidationError
            {
                Code = "VAL_010",
                Message = "测试项目没有配置任何步骤",
                Severity = ErrorSeverity.Critical
            });
            return;
        }

        var stepIds = new HashSet<string>();
        var stepNames = new HashSet<string>();

        foreach (var step in project.Steps)
        {
            // 验证步骤 ID
            if (string.IsNullOrWhiteSpace(step.Id))
            {
                report.Errors.Add(new ValidationError
                {
                    Code = "VAL_011",
                    Message = $"步骤 ID 不能为空 (Order: {step.Order})",
                    Severity = ErrorSeverity.High,
                    StepId = step.Id
                });
            }
            else if (!IsValidId(step.Id))
            {
                report.Errors.Add(new ValidationError
                {
                    Code = "VAL_012",
                    Message = $"步骤 ID 格式无效: {step.Id}",
                    Severity = ErrorSeverity.Medium,
                    StepId = step.Id,
                    Suggestion = "ID 应仅包含字母、数字、下划线和连字符"
                });
            }
            else if (stepIds.Contains(step.Id))
            {
                report.Errors.Add(new ValidationError
                {
                    Code = "VAL_013",
                    Message = $"步骤 ID 重复: {step.Id}",
                    Severity = ErrorSeverity.High,
                    StepId = step.Id
                });
            }
            else
            {
                stepIds.Add(step.Id);
            }

            // 验证步骤名称
            if (string.IsNullOrWhiteSpace(step.Name))
            {
                report.Errors.Add(new ValidationError
                {
                    Code = "VAL_014",
                    Message = $"步骤名称不能为空 (ID: {step.Id})",
                    Severity = ErrorSeverity.High,
                    StepId = step.Id
                });
            }
            else if (stepNames.Contains(step.Name))
            {
                report.Warnings.Add(new ValidationWarning
                {
                    Code = "VAL_W002",
                    Message = $"步骤名称重复: {step.Name}",
                    StepId = step.Id,
                    Suggestion = "建议使用唯一的步骤名称以便区分"
                });
            }
            else
            {
                stepNames.Add(step.Name);
            }

            // 验证步骤类型
            if (string.IsNullOrWhiteSpace(step.Type))
            {
                report.Errors.Add(new ValidationError
                {
                    Code = "VAL_015",
                    Message = $"步骤类型不能为空 (ID: {step.Id})",
                    Severity = ErrorSeverity.High,
                    StepId = step.Id
                });
            }
            else if (!IsValidStepType(step.Type))
            {
                report.Warnings.Add(new ValidationWarning
                {
                    Code = "VAL_W003",
                    Message = $"步骤类型可能不受支持: {step.Type} (ID: {step.Id})",
                    StepId = step.Id,
                    Suggestion = "常用类型: serial, custom, cmd, network, instrument"
                });
            }

            // 验证命令
            if (string.IsNullOrWhiteSpace(step.Command))
            {
                report.Errors.Add(new ValidationError
                {
                    Code = "VAL_016",
                    Message = $"步骤命令不能为空 (ID: {step.Id})",
                    Severity = ErrorSeverity.High,
                    StepId = step.Id
                });
            }

            // 验证通道
            if (string.IsNullOrWhiteSpace(step.Channel))
            {
                report.Warnings.Add(new ValidationWarning
                {
                    Code = "VAL_W004",
                    Message = $"步骤未指定通道 (ID: {step.Id})",
                    StepId = step.Id,
                    Suggestion = "建议明确指定通道: Serial, Cmd, Network 等"
                });
            }
        }
    }

    /// <summary>
    /// 验证步骤顺序
    /// </summary>
    private void ValidateStepOrder(ConfigTestProject project, ValidationReport report)
    {
        if (project.Steps == null || !project.Steps.Any())
        {
            return;
        }

        var orders = project.Steps.Select(s => s.Order).ToList();
        var duplicateOrders = orders.GroupBy(o => o)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicateOrders.Any())
        {
            report.Warnings.Add(new ValidationWarning
            {
                Code = "VAL_W005",
                Message = $"存在重复的步骤顺序: {string.Join(", ", duplicateOrders)}",
                Suggestion = "建议使用唯一的顺序号以明确执行顺序"
            });
        }

        // 检查顺序是否连续
        var sortedOrders = orders.OrderBy(o => o).ToList();
        for (int i = 1; i < sortedOrders.Count; i++)
        {
            if (sortedOrders[i] - sortedOrders[i - 1] > 10)
            {
                report.Warnings.Add(new ValidationWarning
                {
                    Code = "VAL_W006",
                    Message = $"步骤顺序存在较大间隔: {sortedOrders[i - 1]} -> {sortedOrders[i]}",
                    Suggestion = "建议使用连续或接近的顺序号"
                });
                break;
            }
        }
    }

    /// <summary>
    /// 验证步骤依赖
    /// </summary>
    private void ValidateStepDependencies(ConfigTestProject project, ValidationReport report)
    {
        if (project.Steps == null || !project.Steps.Any())
        {
            return;
        }

        // 检查是否有步骤设置了 ContinueOnFailure
        var criticalSteps = project.Steps.Where(s => !s.ContinueOnFailure).ToList();
        if (criticalSteps.Count == project.Steps.Count)
        {
            report.Warnings.Add(new ValidationWarning
            {
                Code = "VAL_W007",
                Message = "所有步骤都设置为失败时停止",
                Suggestion = "考虑将某些非关键步骤设置为 ContinueOnFailure = true"
            });
        }
    }

    /// <summary>
    /// 验证超时设置
    /// </summary>
    private void ValidateTimeouts(ConfigTestProject project, ValidationReport report)
    {
        if (project.Steps == null || !project.Steps.Any())
        {
            return;
        }

        foreach (var step in project.Steps)
        {
            if (!step.Timeout.HasValue || step.Timeout.Value <= 0)
            {
                report.Warnings.Add(new ValidationWarning
                {
                    Code = "VAL_W008",
                    Message = $"步骤未设置超时或超时无效 (ID: {step.Id})",
                    StepId = step.Id,
                    Suggestion = "建议设置合理的超时时间（5000-30000ms）"
                });
            }
            else if (step.Timeout.Value < 1000)
            {
                report.Warnings.Add(new ValidationWarning
                {
                    Code = "VAL_W009",
                    Message = $"步骤超时时间过短: {step.Timeout.Value}ms (ID: {step.Id})",
                    StepId = step.Id,
                    Suggestion = "建议超时时间至少 1000ms"
                });
            }
            else if (step.Timeout.Value > 60000)
            {
                report.Warnings.Add(new ValidationWarning
                {
                    Code = "VAL_W010",
                    Message = $"步骤超时时间过长: {step.Timeout.Value}ms (ID: {step.Id})",
                    StepId = step.Id,
                    Suggestion = "建议超时时间不超过 60000ms"
                });
            }
        }
    }

    /// <summary>
    /// 验证命令格式
    /// </summary>
    private void ValidateCommands(ConfigTestProject project, ValidationReport report)
    {
        if (project.Steps == null || !project.Steps.Any())
        {
            return;
        }

        foreach (var step in project.Steps)
        {
            if (string.IsNullOrWhiteSpace(step.Command))
            {
                continue;
            }

            // 检查危险命令
            var dangerousPatterns = new[]
            {
                "rm -rf", "del /f", "format", "fdisk",
                "shutdown", "reboot", "init 0", "init 6"
            };

            foreach (var pattern in dangerousPatterns)
            {
                if (step.Command.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    report.Warnings.Add(new ValidationWarning
                    {
                        Code = "VAL_W011",
                        Message = $"步骤包含潜在危险命令: {pattern} (ID: {step.Id})",
                        StepId = step.Id,
                        Suggestion = "请确认命令的安全性"
                    });
                }
            }

            // 检查命令长度
            if (step.Command.Length > 500)
            {
                report.Warnings.Add(new ValidationWarning
                {
                    Code = "VAL_W012",
                    Message = $"步骤命令过长: {step.Command.Length} 字符 (ID: {step.Id})",
                    StepId = step.Id,
                    Suggestion = "考虑将复杂命令封装为脚本"
                });
            }
        }
    }

    /// <summary>
    /// 验证期望值格式
    /// </summary>
    private void ValidateExpectedValues(ConfigTestProject project, ValidationReport report)
    {
        if (project.Steps == null || !project.Steps.Any())
        {
            return;
        }

        foreach (var step in project.Steps)
        {
            if (string.IsNullOrWhiteSpace(step.Expected))
            {
                report.Warnings.Add(new ValidationWarning
                {
                    Code = "VAL_W013",
                    Message = $"步骤未设置期望值 (ID: {step.Id})",
                    StepId = step.Id,
                    Suggestion = "建议设置期望值以验证执行结果"
                });
                continue;
            }

            // 验证正则表达式格式
            if (step.Expected.StartsWith("regex:", StringComparison.OrdinalIgnoreCase))
            {
                var pattern = step.Expected.Substring("regex:".Length);
                try
                {
                    _ = new Regex(pattern);
                }
                catch (ArgumentException)
                {
                    report.Errors.Add(new ValidationError
                    {
                        Code = "VAL_020",
                        Message = $"步骤期望值正则表达式无效: {pattern} (ID: {step.Id})",
                        Severity = ErrorSeverity.High,
                        StepId = step.Id,
                        Suggestion = "请检查正则表达式语法"
                    });
                }
            }
        }
    }

    /// <summary>
    /// 验证变量模板引用 — 确保 {{key}} 在之前步骤的 StoreResultAs 中已生成
    /// </summary>
    private void ValidateVariableTemplates(ConfigTestProject project, ValidationReport report)
    {
        if (project.Steps == null || !project.Steps.Any())
        {
            return;
        }

        var templateRegex = new Regex(@"\{\{\s*([\w\.:\-]+)\s*\}\}|\$\{\s*([\w\.:\-]+)\s*\}");
        var sortedSteps = project.Steps.OrderBy(s => s.Order).ToList();
        var availableKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var step in sortedSteps)
        {
            // 检查 Command 中的模板变量引用
            CheckFieldTemplateReferences(step.Command, "Command", step, availableKeys, templateRegex, report);

            // 检查 Expected 中的模板变量引用
            CheckFieldTemplateReferences(step.Expected, "Expected", step, availableKeys, templateRegex, report);

            // 注册此步骤产出的变量
            if (!string.IsNullOrWhiteSpace(step.StoreResultAs))
            {
                availableKeys.Add(step.StoreResultAs);
            }
        }
    }

    private void CheckFieldTemplateReferences(
        string? fieldValue,
        string fieldName,
        ConfigTestStep step,
        HashSet<string> availableKeys,
        Regex templateRegex,
        ValidationReport report)
    {
        if (string.IsNullOrWhiteSpace(fieldValue))
        {
            return;
        }

        var matches = templateRegex.Matches(fieldValue);
        foreach (Match match in matches)
        {
            var key = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
            if (!availableKeys.Contains(key))
            {
                report.Errors.Add(new ValidationError
                {
                    Code = "VAL_030",
                    Message = $"步骤 {fieldName} 引用了未定义的变量 '{{{{{key}}}}}' (ID: {step.Id})",
                    Severity = ErrorSeverity.High,
                    StepId = step.Id,
                    Suggestion = $"请确保在此步骤之前有步骤通过 StoreResultAs 产出 '{key}'"
                });
            }
        }
    }

    /// <summary>
    /// 验证条件表达式格式和引用
    /// </summary>
    private void ValidateConditionExpressions(ConfigTestProject project, ValidationReport report)
    {
        if (project.Steps == null || !project.Steps.Any())
        {
            return;
        }

        var sortedSteps = project.Steps.OrderBy(s => s.Order).ToList();
        var availableKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var step in sortedSteps)
        {
            if (!string.IsNullOrWhiteSpace(step.ConditionExpression))
            {
                ValidateSingleCondition(step.ConditionExpression, step, availableKeys, report);
            }

            if (!string.IsNullOrWhiteSpace(step.StoreResultAs))
            {
                availableKeys.Add(step.StoreResultAs);
            }
        }
    }

    private void ValidateSingleCondition(
        string expression,
        ConfigTestStep step,
        HashSet<string> availableKeys,
        ValidationReport report)
    {
        // 支持的前缀：exists: / notexists: / equals:key:value / contains:key:value
        var validPrefixes = new[] { "exists:", "notexists:", "equals:", "contains:" };
        bool matched = false;

        foreach (var prefix in validPrefixes)
        {
            if (expression.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                matched = true;
                var remainder = expression.Substring(prefix.Length);

                if (prefix == "exists:" || prefix == "notexists:")
                {
                    // remainder 应该是一个 key
                    if (string.IsNullOrWhiteSpace(remainder))
                    {
                        report.Errors.Add(new ValidationError
                        {
                            Code = "VAL_031",
                            Message = $"条件表达式缺少变量名: '{expression}' (ID: {step.Id})",
                            Severity = ErrorSeverity.High,
                            StepId = step.Id,
                            Suggestion = "格式应为 exists:<key> 或 notexists:<key>"
                        });
                    }
                    else if (!availableKeys.Contains(remainder))
                    {
                        report.Warnings.Add(new ValidationWarning
                        {
                            Code = "VAL_W030",
                            Message = $"条件表达式引用的变量 '{remainder}' 在之前步骤中未通过 StoreResultAs 定义 (ID: {step.Id})",
                            StepId = step.Id,
                            Suggestion = "请确认变量在运行时会由之前步骤产出"
                        });
                    }
                }
                else
                {
                    // equals:key:value / contains:key:value — 需要至少两段
                    var parts = remainder.Split(':', 2);
                    if (parts.Length < 2 || string.IsNullOrWhiteSpace(parts[0]))
                    {
                        report.Errors.Add(new ValidationError
                        {
                            Code = "VAL_032",
                            Message = $"条件表达式格式不正确: '{expression}' (ID: {step.Id})",
                            Severity = ErrorSeverity.High,
                            StepId = step.Id,
                            Suggestion = $"格式应为 {prefix}<key>:<value>"
                        });
                    }
                    else if (!availableKeys.Contains(parts[0]))
                    {
                        report.Warnings.Add(new ValidationWarning
                        {
                            Code = "VAL_W031",
                            Message = $"条件表达式引用的变量 '{parts[0]}' 在之前步骤中未通过 StoreResultAs 定义 (ID: {step.Id})",
                            StepId = step.Id,
                            Suggestion = "请确认变量在运行时会由之前步骤产出"
                        });
                    }
                }

                break;
            }
        }

        if (!matched)
        {
            report.Errors.Add(new ValidationError
            {
                Code = "VAL_033",
                Message = $"不支持的条件表达式前缀: '{expression}' (ID: {step.Id})",
                Severity = ErrorSeverity.Medium,
                StepId = step.Id,
                Suggestion = "支持的前缀: exists:, notexists:, equals:, contains:"
            });
        }
    }

    /// <summary>
    /// 验证 StoreResultAs 依赖关系 — 检测重复键和循环依赖
    /// </summary>
    private void ValidateStoreResultDependencies(ConfigTestProject project, ValidationReport report)
    {
        if (project.Steps == null || !project.Steps.Any())
        {
            return;
        }

        var producerMap = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var step in project.Steps)
        {
            if (string.IsNullOrWhiteSpace(step.StoreResultAs))
            {
                continue;
            }

            if (!producerMap.TryGetValue(step.StoreResultAs, out var producers))
            {
                producers = new List<string>();
                producerMap[step.StoreResultAs] = producers;
            }
            producers.Add(step.Id);
        }

        // 检测同一个键被多个步骤产出（后者会覆盖前者）
        foreach (var kvp in producerMap)
        {
            if (kvp.Value.Count > 1)
            {
                report.Warnings.Add(new ValidationWarning
                {
                    Code = "VAL_W032",
                    Message = $"变量 '{kvp.Key}' 被多个步骤产出: {string.Join(", ", kvp.Value)}，后续步骤会覆盖之前的值",
                    Suggestion = "如果非预期行为，请使用不同的 StoreResultAs 键名"
                });
            }
        }
    }

    /// <summary>
    /// 验证 ValidationRules 结构完整性
    /// </summary>
    private void ValidateValidationRules(ConfigTestProject project, ValidationReport report)
    {
        if (project.Steps == null || !project.Steps.Any())
        {
            return;
        }

        foreach (var step in project.Steps)
        {
            if (step.ValidationRules == null || step.ValidationRules.Count == 0)
            {
                continue;
            }

            var rules = step.ValidationRules;

            // NumericRange 检查
            if (rules.TryGetValue("NumericRange", out var rangeObj) && rangeObj is IDictionary rangeDict)
            {
                bool hasMin = false;
                bool hasMax = false;
                double minVal = 0;
                double maxVal = 0;

                foreach (DictionaryEntry entry in rangeDict)
                {
                    var key = entry.Key?.ToString();
                    if (string.Equals(key, "Min", StringComparison.OrdinalIgnoreCase))
                    {
                        hasMin = double.TryParse(
                            Convert.ToString(entry.Value, CultureInfo.InvariantCulture),
                            NumberStyles.Any,
                            CultureInfo.InvariantCulture,
                            out minVal);
                    }
                    else if (string.Equals(key, "Max", StringComparison.OrdinalIgnoreCase))
                    {
                        hasMax = double.TryParse(
                            Convert.ToString(entry.Value, CultureInfo.InvariantCulture),
                            NumberStyles.Any,
                            CultureInfo.InvariantCulture,
                            out maxVal);
                    }
                }

                if (!hasMin || !hasMax)
                {
                    report.Errors.Add(new ValidationError
                    {
                        Code = "VAL_040",
                        Message = $"步骤 ValidationRules.NumericRange 缺少 Min 或 Max (ID: {step.Id})",
                        Severity = ErrorSeverity.High,
                        StepId = step.Id,
                        Suggestion = "NumericRange 必须包含 Min 和 Max 数值字段"
                    });
                }
                else if (minVal > maxVal)
                {
                    report.Errors.Add(new ValidationError
                    {
                        Code = "VAL_041",
                        Message = $"步骤 ValidationRules.NumericRange Min({minVal}) > Max({maxVal}) (ID: {step.Id})",
                        Severity = ErrorSeverity.High,
                        StepId = step.Id,
                        Suggestion = "请确保 Min <= Max"
                    });
                }
            }

            // MustContainAll 检查 — 应为数组
            if (rules.TryGetValue("MustContainAll", out var containAll))
            {
                if (containAll is not IEnumerable || containAll is string)
                {
                    report.Errors.Add(new ValidationError
                    {
                        Code = "VAL_042",
                        Message = $"步骤 ValidationRules.MustContainAll 应为数组 (ID: {step.Id})",
                        Severity = ErrorSeverity.Medium,
                        StepId = step.Id,
                        Suggestion = "格式示例: \"MustContainAll\": [\"OK\", \"PASS\"]"
                    });
                }
            }

            // MustNotContainAny 检查 — 应为数组
            if (rules.TryGetValue("MustNotContainAny", out var notContain))
            {
                if (notContain is not IEnumerable || notContain is string)
                {
                    report.Errors.Add(new ValidationError
                    {
                        Code = "VAL_043",
                        Message = $"步骤 ValidationRules.MustNotContainAny 应为数组 (ID: {step.Id})",
                        Severity = ErrorSeverity.Medium,
                        StepId = step.Id,
                        Suggestion = "格式示例: \"MustNotContainAny\": [\"ERROR\", \"FAIL\"]"
                    });
                }
            }

            // Regex 检查 — 尝试编译
            if (rules.TryGetValue("Regex", out var regexObj))
            {
                var pattern = regexObj?.ToString();
                if (!string.IsNullOrWhiteSpace(pattern))
                {
                    try
                    {
                        _ = new Regex(pattern);
                    }
                    catch (ArgumentException)
                    {
                        report.Errors.Add(new ValidationError
                        {
                            Code = "VAL_044",
                            Message = $"步骤 ValidationRules.Regex 正则表达式无效: '{pattern}' (ID: {step.Id})",
                            Severity = ErrorSeverity.High,
                            StepId = step.Id,
                            Suggestion = "请检查正则表达式语法"
                        });
                    }
                }
            }
        }
    }

    /// <summary>
    /// 验证步骤 Type 与 Channel 组合的一致性
    /// </summary>
    private void ValidateChannelTypeConsistency(ConfigTestProject project, ValidationReport report)
    {
        if (project.Steps == null || !project.Steps.Any())
        {
            return;
        }

        // Type → 常见通道的映射
        var typeChannelHints = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["serial"] = new[] { "serial", "uart", "rs232", "rs485", "com" },
            ["telnet"] = new[] { "telnet", "network", "tcp" },
            ["scpi"] = new[] { "scpi", "lxi", "gpib", "visa", "instrument" },
            ["adb"] = new[] { "adb", "usb", "android" },
            ["cmd"] = new[] { "cmd", "shell", "process", "local" },
            ["ssh"] = new[] { "ssh", "network" },
            ["http"] = new[] { "http", "https", "rest", "api" }
        };

        foreach (var step in project.Steps)
        {
            if (string.IsNullOrWhiteSpace(step.Type) || string.IsNullOrWhiteSpace(step.Channel))
            {
                continue;
            }

            var type = step.Type.ToLower();
            if (typeChannelHints.TryGetValue(type, out var expectedChannels))
            {
                var channel = step.Channel.ToLower();
                bool channelMatches = expectedChannels.Any(c => channel.Contains(c));
                if (!channelMatches)
                {
                    report.Warnings.Add(new ValidationWarning
                    {
                        Code = "VAL_W040",
                        Message = $"步骤类型 '{step.Type}' 与通道 '{step.Channel}' 可能不匹配 (ID: {step.Id})",
                        StepId = step.Id,
                        Suggestion = $"类型 '{step.Type}' 通常使用的通道: {string.Join(", ", expectedChannels)}"
                    });
                }
            }
        }
    }

    /// <summary>
    /// 验证 ID 格式
    /// </summary>
    private bool IsValidId(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        // ID 应仅包含字母、数字、下划线和连字符
        return Regex.IsMatch(id, @"^[a-zA-Z0-9_-]+$");
    }

    /// <summary>
    /// 验证步骤类型
    /// </summary>
    private bool IsValidStepType(string type)
    {
        var validTypes = new[]
        {
            "serial", "custom", "cmd", "network", "instrument",
            "adb", "scpi", "http", "ssh", "telnet"
        };

        return validTypes.Contains(type.ToLower());
    }

    /// <summary>
    /// 生成验证报告摘要
    /// </summary>
    public string GenerateReportSummary(ValidationReport report)
    {
        var summary = new System.Text.StringBuilder();

        summary.AppendLine($"=== 验证报告 ===");
        summary.AppendLine($"项目: {report.ProjectName} ({report.ProjectId})");
        summary.AppendLine($"结果: {(report.IsValid ? "✅ 通过" : "❌ 失败")}");
        summary.AppendLine($"错误: {report.Errors.Count}");
        summary.AppendLine($"警告: {report.Warnings.Count}");
        summary.AppendLine();

        if (report.Errors.Any())
        {
            summary.AppendLine("错误列表:");
            foreach (var error in report.Errors)
            {
                summary.AppendLine($"  [{error.Code}] {error.Message}");
                if (!string.IsNullOrEmpty(error.Suggestion))
                {
                    summary.AppendLine($"    建议: {error.Suggestion}");
                }
            }
            summary.AppendLine();
        }

        if (report.Warnings.Any())
        {
            summary.AppendLine("警告列表:");
            foreach (var warning in report.Warnings)
            {
                summary.AppendLine($"  [{warning.Code}] {warning.Message}");
                if (!string.IsNullOrEmpty(warning.Suggestion))
                {
                    summary.AppendLine($"    建议: {warning.Suggestion}");
                }
            }
        }

        return summary.ToString();
    }
}

/// <summary>
/// 验证报告
/// </summary>
public class ValidationReport
{
    public string ProjectId { get; set; } = "";
    public string ProjectName { get; set; } = "";
    public bool IsValid { get; set; }
    public List<ValidationError> Errors { get; set; } = new();
    public List<ValidationWarning> Warnings { get; set; } = new();
}

/// <summary>
/// 验证错误
/// </summary>
public class ValidationError
{
    public string Code { get; set; } = "";
    public string Message { get; set; } = "";
    public ErrorSeverity Severity { get; set; }
    public string? StepId { get; set; }
    public string? Suggestion { get; set; }
}

/// <summary>
/// 验证警告
/// </summary>
public class ValidationWarning
{
    public string Code { get; set; } = "";
    public string Message { get; set; } = "";
    public string? StepId { get; set; }
    public string? Suggestion { get; set; }
}

/// <summary>
/// 错误严重程度
/// </summary>
public enum ErrorSeverity
{
    Low,        // 低
    Medium,     // 中
    High,       // 高
    Critical    // 严重
}
