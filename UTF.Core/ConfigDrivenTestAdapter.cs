using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UTF.Logging;

namespace UTF.Core;

/// <summary>
/// 配置驱动测试适配器 - 将统一配置模型转换为测试引擎可用的格式
/// </summary>
public sealed class ConfigDrivenTestAdapter
{
    private readonly ILogger _logger;

    public ConfigDrivenTestAdapter(ILogger? logger = null)
    {
        _logger = logger ?? LoggerFactory.CreateLogger<ConfigDrivenTestAdapter>();
    }

    /// <summary>
    /// 从 UnifiedConfiguration 的 TestStepConfig 转换为 ConfigTestStep
    /// </summary>
    public ConfigTestStep ConvertToConfigTestStep(object stepConfig)
    {
        try
        {
            // 使用反射获取属性值
            var type = stepConfig.GetType();

            var step = new ConfigTestStep
            {
                Id = GetPropertyValue<string>(stepConfig, "Id") ?? Guid.NewGuid().ToString(),
                Name = GetPropertyValue<string>(stepConfig, "Name") ?? "未命名步骤",
                Description = GetPropertyValue<string>(stepConfig, "Description") ?? "",
                Order = GetPropertyValue<int?>(stepConfig, "Order") ?? 1,
                Enabled = GetPropertyValue<bool?>(stepConfig, "Enabled") ?? true,
                Type = GetPropertyValue<string>(stepConfig, "Type")
                    ?? GetPropertyValue<string>(stepConfig, "CommandType"),
                TargetDeviceId = GetPropertyValue<string>(stepConfig, "TargetDeviceId"),
                Command = GetPropertyValue<string>(stepConfig, "Command") ?? "",
                Expected = GetPropertyValue<string>(stepConfig, "Expected")
                    ?? GetPropertyValue<string>(stepConfig, "ExpectedResult"),
                Timeout = GetPropertyValue<int?>(stepConfig, "Timeout"),
                Delay = GetPropertyValue<int?>(stepConfig, "Delay")
                    ?? GetPropertyValue<int?>(stepConfig, "PostExecutionDelay"),
                RetryCount = GetPropertyValue<int?>(stepConfig, "RetryCount"),
                Channel = GetPropertyValue<string>(stepConfig, "Channel")
                    ?? GetPropertyValue<string>(stepConfig, "ChannelOverride"),
                StoreResultAs = GetPropertyValue<string>(stepConfig, "StoreResultAs"),
                ConditionExpression = GetPropertyValue<string>(stepConfig, "ConditionExpression"),
                ContinueOnFailure = GetPropertyValue<bool?>(stepConfig, "ContinueOnFailure") ?? false
            };

            // 转换 ValidationRules
            var validationRules = GetPropertyValue<object>(stepConfig, "ValidationRules");
            if (TryConvertDictionary(validationRules, out var validationRulesDict))
            {
                step.ValidationRules = validationRulesDict;
            }

            // 转换 Parameters
            var parameters = GetPropertyValue<object>(stepConfig, "Parameters");
            if (TryConvertDictionary(parameters, out var parametersDict))
            {
                step.Parameters = parametersDict;
            }

            return step;
        }
        catch (Exception ex)
        {
            _logger.Warning($"转换测试步骤失败: {ex.Message}");
            return new ConfigTestStep
            {
                Id = Guid.NewGuid().ToString(),
                Name = "转换失败的步骤",
                Enabled = false
            };
        }
    }

    /// <summary>
    /// 从 UnifiedConfiguration 的 TestProject 转换为 ConfigTestProject
    /// </summary>
    public ConfigTestProject ConvertToConfigTestProject(object testProject)
    {
        try
        {
            var project = new ConfigTestProject
            {
                Id = GetPropertyValue<string>(testProject, "Id") ?? "default_project",
                Name = GetPropertyValue<string>(testProject, "Name") ?? "未命名测试项目",
                Description = GetPropertyValue<string>(testProject, "Description") ?? "",
                Enabled = GetPropertyValue<bool?>(testProject, "Enabled") ?? true,
                Steps = new List<ConfigTestStep>()
            };

            // 转换测试步骤
            var steps = GetPropertyValue<object>(testProject, "Steps");
            if (steps != null)
            {
                if (steps is System.Collections.IEnumerable enumerable)
                {
                    foreach (var stepObj in enumerable)
                    {
                        if (stepObj != null)
                        {
                            var step = ConvertToConfigTestStep(stepObj);
                            project.Steps.Add(step);
                        }
                    }
                }
            }

            return project;
        }
        catch (Exception ex)
        {
            _logger.Error($"转换测试项目失败: {ex.Message}", ex);
            return new ConfigTestProject
            {
                Id = "error_project",
                Name = "转换失败的项目",
                Enabled = false
            };
        }
    }

    /// <summary>
    /// 将 ConfigDrivenStepResult 转换为 TestStepResultData
    /// </summary>
    public TestStepResultData ConvertToTestStepResultData(ConfigDrivenStepResult stepResult)
    {
        return new TestStepResultData
        {
            StepId = stepResult.StepId,
            StepName = stepResult.StepName,
            Passed = stepResult.Passed,
            MeasuredValue = stepResult.MeasuredValue,
            ExpectedValue = stepResult.ExpectedValue,
            ErrorMessage = stepResult.ErrorMessage,
            StartTime = stepResult.StartTime,
            EndTime = stepResult.EndTime,
            RetryCount = stepResult.RetryCount,
            ExtendedData = new Dictionary<string, object>
            {
                { "RawOutput", stepResult.RawOutput },
                { "Skipped", stepResult.Skipped }
            }
        };
    }

    /// <summary>
    /// 将 ConfigDrivenTestReport 转换为 TestReport
    /// </summary>
    public TestReport ConvertToTestReport(ConfigDrivenTestReport report)
    {
        return new TestReport
        {
            ReportId = Guid.NewGuid().ToString(),
            TaskId = report.ProjectId,
            DUTId = report.DutId,
            OverallResult = report.Passed,
            StepResults = report.StepResults.Select(ConvertToTestStepExecutionResult).ToList(),
            StartTime = report.StartTime,
            EndTime = report.EndTime,
            Operator = "System",
            TestStation = Environment.MachineName
        };
    }

    /// <summary>
    /// 将 ConfigDrivenStepResult 转换为 TestStepExecutionResult
    /// </summary>
    private TestStepExecutionResult ConvertToTestStepExecutionResult(ConfigDrivenStepResult stepResult)
    {
        return new TestStepExecutionResult
        {
            StepId = stepResult.StepId,
            StepName = stepResult.StepName,
            Passed = stepResult.Passed,
            MeasuredValue = stepResult.MeasuredValue,
            ExpectedValue = stepResult.ExpectedValue,
            ErrorMessage = stepResult.ErrorMessage,
            StartTime = stepResult.StartTime,
            EndTime = stepResult.EndTime,
            RetryCount = stepResult.RetryCount,
            ExtendedData = new Dictionary<string, object>
            {
                { "RawOutput", stepResult.RawOutput },
                { "Skipped", stepResult.Skipped }
            }
        };
    }

    /// <summary>
    /// 获取对象属性值（泛型版本）
    /// </summary>
    private T? GetPropertyValue<T>(object obj, string propertyName)
    {
        try
        {
            var type = obj.GetType();
            var property = type.GetProperty(propertyName);

            if (property != null)
            {
                var value = property.GetValue(obj);
                if (value is T typedValue)
                {
                    return typedValue;
                }

                // 尝试类型转换
                if (value != null && typeof(T) != typeof(object))
                {
                    try
                    {
                        return (T)Convert.ChangeType(value, typeof(T));
                    }
                    catch
                    {
                        // 转换失败，返回默认值
                    }
                }
            }

            return default;
        }
        catch
        {
            return default;
        }
    }

    private static bool TryConvertDictionary(object? raw, out Dictionary<string, object> result)
    {
        result = new Dictionary<string, object>();
        if (raw == null)
        {
            return false;
        }

        if (raw is Dictionary<string, object> typedDictionary)
        {
            result = new Dictionary<string, object>(typedDictionary);
            return true;
        }

        if (raw is IDictionary dictionary)
        {
            foreach (DictionaryEntry entry in dictionary)
            {
                var key = Convert.ToString(entry.Key, CultureInfo.InvariantCulture);
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                result[key] = entry.Value ?? string.Empty;
            }

            return result.Count > 0;
        }

        return false;
    }

    /// <summary>
    /// 批量转换测试步骤
    /// </summary>
    public List<ConfigTestStep> ConvertTestSteps(IEnumerable<object> stepConfigs)
    {
        var steps = new List<ConfigTestStep>();

        foreach (var stepConfig in stepConfigs)
        {
            try
            {
                var step = ConvertToConfigTestStep(stepConfig);
                steps.Add(step);
            }
            catch (Exception ex)
            {
                _logger.Warning($"转换测试步骤失败: {ex.Message}");
            }
        }

        return steps;
    }

    /// <summary>
    /// 验证转换后的测试项目
    /// </summary>
    public bool ValidateConvertedProject(ConfigTestProject project)
    {
        if (string.IsNullOrWhiteSpace(project.Id))
        {
            _logger.Error("测试项目 ID 为空");
            return false;
        }

        if (string.IsNullOrWhiteSpace(project.Name))
        {
            _logger.Error("测试项目名称为空");
            return false;
        }

        if (project.Steps == null || !project.Steps.Any())
        {
            _logger.Error("测试项目没有步骤");
            return false;
        }

        foreach (var step in project.Steps)
        {
            if (string.IsNullOrWhiteSpace(step.Id))
            {
                _logger.Error($"测试步骤 ID 为空: {step.Name}");
                return false;
            }

            if (string.IsNullOrWhiteSpace(step.Command))
            {
                _logger.Warning($"测试步骤命令为空: {step.Name}");
            }
        }

        return true;
    }
}
