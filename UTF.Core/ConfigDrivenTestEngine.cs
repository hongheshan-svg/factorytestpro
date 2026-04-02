using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UTF.Core.Events;
using UTF.Core.Persistence;
using UTF.Logging;
using UTF.Plugin.Abstractions;

namespace UTF.Core;

/// <summary>
/// 配置驱动的测试执行引擎 - 支持灵活的测试步骤配置和插件系统
/// </summary>
public sealed class ConfigDrivenTestEngine : IDisposable
{
    private static readonly Regex TemplateRegex = new(@"\{\{\s*([\w\.:\-]+)\s*\}\}|\$\{\s*([\w\.:\-]+)\s*\}", RegexOptions.Compiled);
    private static readonly Regex FirstNumberRegex = new(@"[-+]?\d*\.?\d+", RegexOptions.Compiled);

    private readonly ILogger _logger;
    private readonly IPluginService? _pluginService;
    private readonly IEventBus? _eventBus;
    private readonly ITestResultRepository? _resultRepository;
    private bool _disposed;

    public ConfigDrivenTestEngine(
        ILogger? logger = null,
        IPluginService? pluginService = null,
        IEventBus? eventBus = null,
        ITestResultRepository? resultRepository = null)
    {
        _logger = logger ?? LoggerFactory.CreateLogger<ConfigDrivenTestEngine>();
        _pluginService = pluginService;
        _eventBus = eventBus;
        _resultRepository = resultRepository;
    }

    /// <summary>
    /// 执行配置驱动的测试步骤
    /// </summary>
    public async Task<ConfigDrivenStepResult> ExecuteStepAsync(
        ConfigTestStep stepConfig,
        string dutId,
        Dictionary<string, object>? context = null,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        var result = new ConfigDrivenStepResult
        {
            StepId = stepConfig.Id,
            StepName = stepConfig.Name,
            StartTime = startTime
        };

        try
        {
            var workingContext = BuildWorkingContext(stepConfig, dutId, context);

            _logger.Info($"执行测试步骤: {stepConfig.Name} (DUT: {dutId})");

            // 检查步骤是否启用
            if (!stepConfig.Enabled)
            {
                _logger.Debug($"跳过禁用的步骤: {stepConfig.Name}");
                result.Passed = true;
                result.Skipped = true;
                result.EndTime = DateTime.UtcNow;
                return result;
            }

            if (!ShouldExecuteStep(stepConfig.ConditionExpression, workingContext, out var conditionReason))
            {
                _logger.Debug($"条件未满足，跳过步骤: {stepConfig.Name}, 条件: {stepConfig.ConditionExpression}");
                result.Passed = true;
                result.Skipped = true;
                result.ErrorMessage = conditionReason;
                result.EndTime = DateTime.UtcNow;
                return result;
            }

            // 执行前延迟
            if (stepConfig.Delay.HasValue && stepConfig.Delay.Value > 0)
            {
                await Task.Delay(stepConfig.Delay.Value, cancellationToken);
            }

            // 重试逻辑
            var maxRetries = ResolveMaxAttempts(stepConfig);

            for (int retry = 0; retry < maxRetries; retry++)
            {
                result.RetryCount = retry;

                try
                {
                    // 执行命令
                    var executionResult = await ExecuteCommandAsync(stepConfig, dutId, workingContext, cancellationToken);
                    result.RawOutput = executionResult.Output;
                    result.MeasuredValue = executionResult.Output;

                    if (!executionResult.Success)
                    {
                        result.Passed = false;
                        result.ErrorMessage = string.IsNullOrWhiteSpace(executionResult.ErrorMessage)
                            ? $"步骤执行失败: {stepConfig.Name}"
                            : executionResult.ErrorMessage;
                    }

                    // 验证结果
                    if (result.Passed || executionResult.Success)
                    {
                        var resolvedExpected = ResolveTemplateString(stepConfig.Expected, workingContext);
                        var resolvedRules = ResolveValidationRules(stepConfig.ValidationRules, workingContext);
                        var validationResult = ValidateResult(executionResult.Output, resolvedExpected, resolvedRules);

                        result.Passed = validationResult.IsValid;
                        result.ErrorMessage = validationResult.ErrorMessage;
                        result.ExpectedValue = resolvedExpected ?? "";
                    }

                    if (result.Passed && !string.IsNullOrWhiteSpace(stepConfig.StoreResultAs))
                    {
                        workingContext[stepConfig.StoreResultAs] = executionResult.Output;
                    }

                    if (result.Passed)
                    {
                        _logger.Debug($"步骤执行成功: {stepConfig.Name}");
                        break;
                    }
                    else if (retry < maxRetries - 1)
                    {
                        _logger.Warning($"步骤失败,准备重试 ({retry + 1}/{maxRetries}): {stepConfig.Name}");
                        await Task.Delay(1000, cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    result.ErrorMessage = ex.Message;
                    _logger.Error($"步骤执行异常: {stepConfig.Name}", ex);

                    if (retry >= maxRetries - 1)
                    {
                        result.Passed = false;
                        break;
                    }
                }
            }

            result.EndTime = DateTime.UtcNow;
            return result;
        }
        catch (Exception ex)
        {
            _logger.Error($"执行测试步骤失败: {stepConfig.Name}", ex);
            result.Passed = false;
            result.ErrorMessage = ex.Message;
            result.EndTime = DateTime.UtcNow;
            return result;
        }
    }

    /// <summary>
    /// 执行命令 - 支持多种通道类型
    /// </summary>
    private async Task<CommandExecutionResult> ExecuteCommandAsync(
        ConfigTestStep stepConfig,
        string dutId,
        Dictionary<string, object>? context,
        CancellationToken cancellationToken)
    {
        var channel = stepConfig.Channel ?? "Serial";
        var stepType = stepConfig.Type ?? "serial";
        var command = ResolveTemplateString(stepConfig.Command, context) ?? "";

        // 如果有插件系统,优先使用插件执行
        if (_pluginService != null && _pluginService.CanHandle(stepType, channel))
        {
            var mergedParameters = new Dictionary<string, object?>();
            if (context != null)
            {
                foreach (var kv in context)
                {
                    mergedParameters[kv.Key] = kv.Value;
                }
            }

            if (stepConfig.Parameters != null)
            {
                foreach (var kv in stepConfig.Parameters)
                {
                    mergedParameters[kv.Key] = kv.Value;
                }
            }

            var request = new StepExecutionRequest
            {
                StepId = stepConfig.Id,
                StepName = stepConfig.Name,
                StepType = stepType,
                Channel = channel,
                Command = command,
                TimeoutMs = stepConfig.Timeout ?? 30000,
                DutId = dutId,
                Parameters = mergedParameters
            };

            var pluginResult = await _pluginService.ExecuteAsync(request, cancellationToken);

            return new CommandExecutionResult
            {
                Success = pluginResult.Status == StepExecutionStatus.Passed,
                Output = pluginResult.NormalizedOutput ?? pluginResult.RawOutput,
                ErrorMessage = pluginResult.ErrorMessage
            };
        }

        // 内置执行逻辑
        return await ExecuteBuiltInCommandAsync(stepConfig, stepType, channel, command, stepConfig.Timeout ?? 30000, cancellationToken);
    }

    /// <summary>
    /// 内置命令执行逻辑
    /// </summary>
    private async Task<CommandExecutionResult> ExecuteBuiltInCommandAsync(
        ConfigTestStep stepConfig,
        string stepType,
        string channel,
        string command,
        int timeoutMs,
        CancellationToken cancellationToken)
    {
        _logger.Debug($"执行内置命令: Type={stepType}, Channel={channel}, Command={command}");

        if (stepConfig.Parameters?.TryGetValue("MockOutput", out var mockOutput) == true)
        {
            return new CommandExecutionResult
            {
                Success = true,
                Output = Convert.ToString(mockOutput, CultureInfo.InvariantCulture) ?? string.Empty
            };
        }

        await Task.Delay(0, cancellationToken);

        return new CommandExecutionResult
        {
            Success = false,
            Output = string.Empty,
            ErrorMessage = $"未找到可处理步骤类型 '{stepType}' 和通道 '{channel}' 的插件。请安装匹配插件或在 Parameters.MockOutput 中提供模拟输出。"
        };
    }

    /// <summary>
    /// 验证测试结果 - 支持多种验证规则
    /// </summary>
    private StepValidationResult ValidateResult(
        string actualOutput,
        string? expectedPattern,
        Dictionary<string, object>? validationRules)
    {
        if (!string.IsNullOrWhiteSpace(expectedPattern))
        {
            var patternValidation = ValidateExpectedPattern(actualOutput, expectedPattern);
            if (!patternValidation.IsValid)
            {
                return patternValidation;
            }
        }

        if (validationRules == null || validationRules.Count == 0)
        {
            return new StepValidationResult { IsValid = true };
        }

        try
        {
            if (TryGetStringList(validationRules, "MustContainAll", out var mustContainAll))
            {
                foreach (var expected in mustContainAll)
                {
                    if (!actualOutput.Contains(expected, StringComparison.OrdinalIgnoreCase))
                    {
                        return new StepValidationResult
                        {
                            IsValid = false,
                            ErrorMessage = $"输出不包含必需内容: {expected}"
                        };
                    }
                }
            }

            if (TryGetStringList(validationRules, "MustNotContainAny", out var mustNotContainAny))
            {
                foreach (var unexpected in mustNotContainAny)
                {
                    if (actualOutput.Contains(unexpected, StringComparison.OrdinalIgnoreCase))
                    {
                        return new StepValidationResult
                        {
                            IsValid = false,
                            ErrorMessage = $"输出包含禁止内容: {unexpected}"
                        };
                    }
                }
            }

            if (TryGetStringRule(validationRules, "Regex", out var regexPattern) &&
                !string.IsNullOrWhiteSpace(regexPattern) &&
                !Regex.IsMatch(actualOutput, regexPattern, RegexOptions.IgnoreCase))
            {
                return new StepValidationResult
                {
                    IsValid = false,
                    ErrorMessage = $"输出不匹配正则表达式: {regexPattern}"
                };
            }

            if (TryGetNumericRange(validationRules, out var minValue, out var maxValue))
            {
                var numberMatch = FirstNumberRegex.Match(actualOutput);
                if (!numberMatch.Success)
                {
                    return new StepValidationResult
                    {
                        IsValid = false,
                        ErrorMessage = "输出中未找到可用于范围校验的数值"
                    };
                }

                if (!double.TryParse(numberMatch.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var actualValue))
                {
                    return new StepValidationResult
                    {
                        IsValid = false,
                        ErrorMessage = "输出中的数值无法解析"
                    };
                }

                if (actualValue < minValue || actualValue > maxValue)
                {
                    return new StepValidationResult
                    {
                        IsValid = false,
                        ErrorMessage = $"数值超出范围: {actualValue} 不在 [{minValue}, {maxValue}]"
                    };
                }
            }

            if (TryGetStringRule(validationRules, "Equals", out var exactValue) &&
                !string.IsNullOrWhiteSpace(exactValue) &&
                !actualOutput.Trim().Equals(exactValue.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return new StepValidationResult
                {
                    IsValid = false,
                    ErrorMessage = $"输出不等于期望值: {exactValue}"
                };
            }

            return new StepValidationResult { IsValid = true };
        }
        catch (Exception ex)
        {
            return new StepValidationResult
            {
                IsValid = false,
                ErrorMessage = $"验证异常: {ex.Message}"
            };
        }
    }

    private StepValidationResult ValidateExpectedPattern(string actualOutput, string expectedPattern)
    {
        if (expectedPattern.StartsWith("contains:", StringComparison.OrdinalIgnoreCase))
        {
            var expectedValue = expectedPattern.Substring("contains:".Length);
            if (actualOutput.Contains(expectedValue, StringComparison.OrdinalIgnoreCase))
            {
                return new StepValidationResult { IsValid = true };
            }

            return new StepValidationResult
            {
                IsValid = false,
                ErrorMessage = $"输出不包含期望值: {expectedValue}"
            };
        }

        if (expectedPattern.StartsWith("notcontains:", StringComparison.OrdinalIgnoreCase))
        {
            var expectedValue = expectedPattern.Substring("notcontains:".Length);
            if (!actualOutput.Contains(expectedValue, StringComparison.OrdinalIgnoreCase))
            {
                return new StepValidationResult { IsValid = true };
            }

            return new StepValidationResult
            {
                IsValid = false,
                ErrorMessage = $"输出包含不允许内容: {expectedValue}"
            };
        }

        if (expectedPattern.StartsWith("equals:", StringComparison.OrdinalIgnoreCase))
        {
            var expectedValue = expectedPattern.Substring("equals:".Length);
            if (actualOutput.Trim().Equals(expectedValue.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return new StepValidationResult { IsValid = true };
            }

            return new StepValidationResult
            {
                IsValid = false,
                ErrorMessage = $"输出不等于期望值: {expectedValue}"
            };
        }

        if (expectedPattern.StartsWith("regex:", StringComparison.OrdinalIgnoreCase))
        {
            var pattern = expectedPattern.Substring("regex:".Length);
            if (Regex.IsMatch(actualOutput, pattern, RegexOptions.IgnoreCase))
            {
                return new StepValidationResult { IsValid = true };
            }

            return new StepValidationResult
            {
                IsValid = false,
                ErrorMessage = $"输出不匹配正则表达式: {pattern}"
            };
        }

        if (actualOutput.Contains(expectedPattern, StringComparison.OrdinalIgnoreCase))
        {
            return new StepValidationResult { IsValid = true };
        }

        return new StepValidationResult
        {
            IsValid = false,
            ErrorMessage = $"输出不包含期望值: {expectedPattern}"
        };
    }

    private static int ResolveMaxAttempts(ConfigTestStep stepConfig)
    {
        var maxAttempts = Math.Max((stepConfig.RetryCount ?? 0) + 1, 1);

        if (stepConfig.Parameters?.TryGetValue("MaxRetries", out var maxRetriesObj) == true &&
            int.TryParse(Convert.ToString(maxRetriesObj, CultureInfo.InvariantCulture), out var maxRetries))
        {
            maxAttempts = Math.Max(maxAttempts, Math.Max(maxRetries, 1));
        }

        return maxAttempts;
    }

    private static Dictionary<string, object> BuildWorkingContext(
        ConfigTestStep stepConfig,
        string dutId,
        Dictionary<string, object>? context)
    {
        var result = context ?? new Dictionary<string, object>();
        result["dutId"] = dutId;
        result["stepId"] = stepConfig.Id;
        result["stepName"] = stepConfig.Name;

        if (!string.IsNullOrWhiteSpace(stepConfig.TargetDeviceId))
        {
            result["targetDeviceId"] = stepConfig.TargetDeviceId;
        }

        if (stepConfig.Parameters != null)
        {
            foreach (var kv in stepConfig.Parameters)
            {
                result[$"step.{kv.Key}"] = kv.Value;
                if (!result.ContainsKey(kv.Key))
                {
                    result[kv.Key] = kv.Value;
                }
            }
        }

        return result;
    }

    private static bool ShouldExecuteStep(string? conditionExpression, Dictionary<string, object> context, out string reason)
    {
        reason = string.Empty;

        if (string.IsNullOrWhiteSpace(conditionExpression))
        {
            return true;
        }

        var expression = conditionExpression.Trim();

        if (expression.StartsWith("exists:", StringComparison.OrdinalIgnoreCase))
        {
            var key = expression.Substring("exists:".Length).Trim();
            var exists = context.ContainsKey(key) && context[key] != null;
            reason = exists ? string.Empty : $"上下文不存在键: {key}";
            return exists;
        }

        if (expression.StartsWith("notexists:", StringComparison.OrdinalIgnoreCase))
        {
            var key = expression.Substring("notexists:".Length).Trim();
            var notExists = !context.ContainsKey(key) || context[key] == null;
            reason = notExists ? string.Empty : $"上下文键存在且不为空: {key}";
            return notExists;
        }

        if (expression.StartsWith("equals:", StringComparison.OrdinalIgnoreCase))
        {
            var parts = expression.Substring("equals:".Length).Split(':', 2);
            if (parts.Length != 2)
            {
                reason = "equals 条件格式错误，应为 equals:key:value";
                return false;
            }

            var key = parts[0].Trim();
            var expected = parts[1].Trim();
            var actual = context.TryGetValue(key, out var value)
                ? Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
                : string.Empty;
            var matched = string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
            reason = matched ? string.Empty : $"条件不满足: {key}={actual} != {expected}";
            return matched;
        }

        if (expression.StartsWith("contains:", StringComparison.OrdinalIgnoreCase))
        {
            var parts = expression.Substring("contains:".Length).Split(':', 2);
            if (parts.Length != 2)
            {
                reason = "contains 条件格式错误，应为 contains:key:value";
                return false;
            }

            var key = parts[0].Trim();
            var expectedPart = parts[1].Trim();
            var actual = context.TryGetValue(key, out var value)
                ? Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
                : string.Empty;
            var matched = actual.Contains(expectedPart, StringComparison.OrdinalIgnoreCase);
            reason = matched ? string.Empty : $"条件不满足: {key} 不包含 {expectedPart}";
            return matched;
        }

        if (bool.TryParse(expression, out var boolLiteral))
        {
            reason = boolLiteral ? string.Empty : "条件表达式为 false";
            return boolLiteral;
        }

        if (context.TryGetValue(expression, out var directValue))
        {
            if (directValue is bool boolValue)
            {
                reason = boolValue ? string.Empty : $"上下文条件键为 false: {expression}";
                return boolValue;
            }

            var valueText = Convert.ToString(directValue, CultureInfo.InvariantCulture);
            var parsed = !string.IsNullOrWhiteSpace(valueText) &&
                         !string.Equals(valueText, "false", StringComparison.OrdinalIgnoreCase) &&
                         !string.Equals(valueText, "0", StringComparison.OrdinalIgnoreCase);
            reason = parsed ? string.Empty : $"上下文条件键值无效: {expression}";
            return parsed;
        }

        reason = $"无法解析条件表达式: {expression}";
        return false;
    }

    private static string? ResolveTemplateString(string? template, Dictionary<string, object>? context)
    {
        if (string.IsNullOrEmpty(template) || context == null || context.Count == 0)
        {
            return template;
        }

        return TemplateRegex.Replace(template, match =>
        {
            var key = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
            return context.TryGetValue(key, out var value)
                ? Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
                : match.Value;
        });
    }

    private static Dictionary<string, object>? ResolveValidationRules(
        Dictionary<string, object>? validationRules,
        Dictionary<string, object> context)
    {
        if (validationRules == null || validationRules.Count == 0)
        {
            return validationRules;
        }

        var result = new Dictionary<string, object>();
        foreach (var kv in validationRules)
        {
            result[kv.Key] = ResolveObject(kv.Value, context);
        }

        return result;
    }

    private static object ResolveObject(object value, Dictionary<string, object> context)
    {
        if (value is string stringValue)
        {
            return ResolveTemplateString(stringValue, context) ?? string.Empty;
        }

        if (value is IDictionary dictionary)
        {
            var converted = new Dictionary<string, object>();
            foreach (DictionaryEntry entry in dictionary)
            {
                var key = Convert.ToString(entry.Key, CultureInfo.InvariantCulture);
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                converted[key] = entry.Value != null ? ResolveObject(entry.Value, context) : string.Empty;
            }

            return converted;
        }

        if (value is IEnumerable enumerable and not string)
        {
            var list = new List<object>();
            foreach (var item in enumerable)
            {
                list.Add(item != null ? ResolveObject(item, context) : string.Empty);
            }

            return list;
        }

        return value;
    }

    private static bool TryGetStringRule(Dictionary<string, object> rules, string key, out string value)
    {
        value = string.Empty;
        if (!rules.TryGetValue(key, out var raw) || raw == null)
        {
            return false;
        }

        value = Convert.ToString(raw, CultureInfo.InvariantCulture) ?? string.Empty;
        return !string.IsNullOrWhiteSpace(value);
    }

    private static bool TryGetStringList(Dictionary<string, object> rules, string key, out List<string> values)
    {
        values = new List<string>();

        if (!rules.TryGetValue(key, out var raw) || raw == null)
        {
            return false;
        }

        if (raw is IEnumerable enumerable and not string)
        {
            foreach (var item in enumerable)
            {
                var text = Convert.ToString(item, CultureInfo.InvariantCulture);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    values.Add(text);
                }
            }
        }
        else
        {
            var text = Convert.ToString(raw, CultureInfo.InvariantCulture);
            if (!string.IsNullOrWhiteSpace(text))
            {
                values.Add(text);
            }
        }

        return values.Count > 0;
    }

    private static bool TryGetNumericRange(Dictionary<string, object> rules, out double min, out double max)
    {
        min = 0;
        max = 0;

        if (!rules.TryGetValue("NumericRange", out var rangeObj) || rangeObj == null)
        {
            return false;
        }

        if (rangeObj is not IDictionary dictionary)
        {
            return false;
        }

        object? minObj = null;
        object? maxObj = null;
        foreach (DictionaryEntry entry in dictionary)
        {
            var key = Convert.ToString(entry.Key, CultureInfo.InvariantCulture);
            if (string.Equals(key, "Min", StringComparison.OrdinalIgnoreCase))
            {
                minObj = entry.Value;
            }

            if (string.Equals(key, "Max", StringComparison.OrdinalIgnoreCase))
            {
                maxObj = entry.Value;
            }
        }

        if (minObj == null || maxObj == null)
        {
            return false;
        }

        var minText = Convert.ToString(minObj, CultureInfo.InvariantCulture);
        var maxText = Convert.ToString(maxObj, CultureInfo.InvariantCulture);

        return double.TryParse(minText, NumberStyles.Float, CultureInfo.InvariantCulture, out min)
            && double.TryParse(maxText, NumberStyles.Float, CultureInfo.InvariantCulture, out max);
    }

    /// <summary>
    /// 执行完整的测试项目
    /// </summary>
    public async Task<ConfigDrivenTestReport> ExecuteTestProjectAsync(
        ConfigTestProject testProject,
        string dutId,
        Dictionary<string, object>? context = null,
        CancellationToken cancellationToken = default)
    {
        var report = new ConfigDrivenTestReport
        {
            ProjectId = testProject.Id,
            ProjectName = testProject.Name,
            DutId = dutId,
            StartTime = DateTime.UtcNow,
            StepResults = new List<ConfigDrivenStepResult>()
        };

        try
        {
            _logger.Info($"开始执行测试项目: {testProject.Name} (DUT: {dutId})");

            if (_eventBus != null)
            {
                await _eventBus.PublishAsync(new TestStartedEvent(dutId, testProject.Id, DateTime.UtcNow));
            }

            if (testProject.Steps == null || !testProject.Steps.Any())
            {
                _logger.Warning($"测试项目没有配置步骤: {testProject.Name}");
                report.Passed = false;
                report.ErrorMessage = "测试项目没有配置步骤";
                report.EndTime = DateTime.UtcNow;
                return report;
            }

            // 按顺序执行测试步骤
            var orderedSteps = testProject.Steps.OrderBy(s => s.Order).ToList();

            foreach (var step in orderedSteps)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    report.ErrorMessage = "测试被取消";
                    break;
                }

                var stepResult = await ExecuteStepAsync(step, dutId, context, cancellationToken);
                report.StepResults.Add(stepResult);

                if (_eventBus != null)
                {
                    await _eventBus.PublishAsync(new TestStepCompletedEvent(
                        dutId, step.Id, stepResult.Passed, DateTime.UtcNow));
                }

                if (!stepResult.Passed && !step.ContinueOnFailure)
                {
                    _logger.Warning($"步骤失败,停止测试: {step.Name}");
                    report.Passed = false;
                    report.ErrorMessage = $"步骤失败: {step.Name}";
                    break;
                }
            }

            // 计算整体结果
            if (string.IsNullOrEmpty(report.ErrorMessage))
            {
                report.Passed = report.StepResults.All(r => r.Passed || r.Skipped);
            }

            report.EndTime = DateTime.UtcNow;
            _logger.Info($"测试项目执行完成: {testProject.Name}, 结果: {(report.Passed ? "PASS" : "FAIL")}");

            if (_eventBus != null)
            {
                await _eventBus.PublishAsync(new TestCompletedEvent(
                    dutId, testProject.Id, report.Passed, DateTime.UtcNow));
            }

            // 持久化测试结果
            if (_resultRepository != null)
            {
                try
                {
                    var persistReport = ConvertToTestReport(report);
                    await _resultRepository.SaveAsync(persistReport, cancellationToken);
                    _logger.Debug($"测试结果已持久化: {persistReport.ReportId}");
                }
                catch (Exception ex)
                {
                    _logger.Warning($"测试结果持久化失败: {ex.Message}");
                }
            }

            return report;
        }
        catch (Exception ex)
        {
            _logger.Error($"执行测试项目失败: {testProject.Name}", ex);
            report.Passed = false;
            report.ErrorMessage = ex.Message;
            report.EndTime = DateTime.UtcNow;
            return report;
        }
    }

    /// <summary>
    /// 将 ConfigDrivenTestReport 转换为持久化用的 TestReport
    /// </summary>
    private static TestReport ConvertToTestReport(ConfigDrivenTestReport source)
    {
        return new TestReport
        {
            ReportId = $"{source.ProjectId}_{source.DutId}_{source.StartTime:yyyyMMdd_HHmmss}",
            TaskId = source.ProjectId,
            DUTId = source.DutId,
            OverallResult = source.Passed,
            StartTime = source.StartTime,
            EndTime = source.EndTime,
            StepResults = source.StepResults.Select(s => new TestStepExecutionResult
            {
                StepId = s.StepId,
                StepName = s.StepName,
                Passed = s.Passed,
                MeasuredValue = s.MeasuredValue,
                ExpectedValue = s.ExpectedValue,
                ErrorMessage = s.ErrorMessage,
                StartTime = s.StartTime,
                EndTime = s.EndTime,
                RetryCount = s.RetryCount
            }).ToList()
        };
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }
}

/// <summary>
/// 配置测试步骤 - 简化的配置模型
/// </summary>
public class ConfigTestStep
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int Order { get; set; } = 1;
    public bool Enabled { get; set; } = true;
    public string? Type { get; set; }
    public string? TargetDeviceId { get; set; }
    public string? Command { get; set; }
    public string? Expected { get; set; }
    public int? Timeout { get; set; }
    public int? Delay { get; set; }
    public int? RetryCount { get; set; }
    public string? Channel { get; set; }
    public string? StoreResultAs { get; set; }
    public string? ConditionExpression { get; set; }
    public bool ContinueOnFailure { get; set; } = false;
    public Dictionary<string, object>? ValidationRules { get; set; }
    public Dictionary<string, object>? Parameters { get; set; }
}

/// <summary>
/// 配置测试项目
/// </summary>
public class ConfigTestProject
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public List<ConfigTestStep>? Steps { get; set; }
}

/// <summary>
/// 配置驱动的步骤结果
/// </summary>
public class ConfigDrivenStepResult
{
    public string StepId { get; set; } = "";
    public string StepName { get; set; } = "";
    public bool Passed { get; set; }
    public bool Skipped { get; set; }
    public string RawOutput { get; set; } = "";
    public string MeasuredValue { get; set; } = "";
    public string ExpectedValue { get; set; } = "";
    public string ErrorMessage { get; set; } = "";
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int RetryCount { get; set; }
}

/// <summary>
/// 配置驱动的测试报告
/// </summary>
public class ConfigDrivenTestReport
{
    public string ProjectId { get; set; } = "";
    public string ProjectName { get; set; } = "";
    public string DutId { get; set; } = "";
    public bool Passed { get; set; }
    public string ErrorMessage { get; set; } = "";
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public List<ConfigDrivenStepResult> StepResults { get; set; } = new();
}

/// <summary>
/// 命令执行结果
/// </summary>
internal class CommandExecutionResult
{
    public bool Success { get; set; }
    public string Output { get; set; } = "";
    public string ErrorMessage { get; set; } = "";
}

/// <summary>
/// 步骤验证结果（内部使用）
/// </summary>
internal class StepValidationResult
{
    public bool IsValid { get; set; }
    public string ErrorMessage { get; set; } = "";
}
