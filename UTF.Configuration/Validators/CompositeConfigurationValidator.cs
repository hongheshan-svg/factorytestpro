using System.Collections.Generic;
using System.Linq;
using UTF.Configuration.Abstractions;
using UTF.Configuration.Models;

namespace UTF.Configuration.Validators;

/// <summary>
/// 组合配置验证器，聚合所有子验证器的结果
/// </summary>
public class CompositeConfigurationValidator
{
    private readonly IConfigurationValidator<SystemConfig> _systemValidator;
    private readonly IConfigurationValidator<DUTConfig> _dutValidator;
    private readonly IConfigurationValidator<TestConfig> _testValidator;

    public CompositeConfigurationValidator(
        IConfigurationValidator<SystemConfig> systemValidator,
        IConfigurationValidator<DUTConfig> dutValidator,
        IConfigurationValidator<TestConfig> testValidator)
    {
        _systemValidator = systemValidator;
        _dutValidator = dutValidator;
        _testValidator = testValidator;
    }

    /// <summary>
    /// 验证所有配置节
    /// </summary>
    public CompositeValidationResult ValidateAll(
        SystemConfig? systemConfig,
        DUTConfig? dutConfig,
        TestConfig? testConfig)
    {
        var sectionResults = new List<SectionValidationResult>();

        if (systemConfig != null)
        {
            var result = _systemValidator.Validate(systemConfig);
            sectionResults.Add(new SectionValidationResult("SystemSettings", result));
        }
        else
        {
            sectionResults.Add(new SectionValidationResult(
                "SystemSettings",
                ConfigValidationResult.Fail("SystemSettings 配置节缺失")));
        }

        if (dutConfig != null)
        {
            var result = _dutValidator.Validate(dutConfig);
            sectionResults.Add(new SectionValidationResult("DUTConfiguration", result));
        }
        else
        {
            sectionResults.Add(new SectionValidationResult(
                "DUTConfiguration",
                ConfigValidationResult.Fail("DUTConfiguration 配置节缺失")));
        }

        if (testConfig != null)
        {
            var result = _testValidator.Validate(testConfig);
            sectionResults.Add(new SectionValidationResult("TestProjectConfiguration", result));
        }
        else
        {
            sectionResults.Add(new SectionValidationResult(
                "TestProjectConfiguration",
                ConfigValidationResult.Fail("TestProjectConfiguration 配置节缺失")));
        }

        return new CompositeValidationResult(sectionResults);
    }
}

/// <summary>
/// 单个配置节的验证结果
/// </summary>
public record SectionValidationResult(string SectionName, ConfigValidationResult Result);

/// <summary>
/// 组合验证结果
/// </summary>
public class CompositeValidationResult
{
    public IReadOnlyList<SectionValidationResult> SectionResults { get; }
    public bool IsValid => SectionResults.All(s => s.Result.IsValid);
    public string[] AllErrors => SectionResults
        .SelectMany(s => s.Result.Errors.Select(e => $"[{s.SectionName}] {e}"))
        .ToArray();

    public CompositeValidationResult(IEnumerable<SectionValidationResult> sectionResults)
    {
        SectionResults = sectionResults.ToList().AsReadOnly();
    }
}
