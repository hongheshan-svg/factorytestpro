using Xunit;
using UTF.Core;
using UTF.Logging;

namespace UTF.Core.Tests;

public class ConfigDrivenTestValidatorTests : IDisposable
{
    private readonly ILogger _logger = LoggerFactory.CreateLogger(
        nameof(ConfigDrivenTestValidatorTests),
        new LogConfiguration
        {
            EnableConsole = false,
            EnableFile = false
        });
    private readonly ConfigDrivenTestValidator _validator;

    public ConfigDrivenTestValidatorTests()
    {
        _validator = new ConfigDrivenTestValidator(logger: _logger);
    }

    public void Dispose()
    {
        if (_logger is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    #region ValidateBasicInfo

    [Fact]
    public void ValidateTestProject_EmptyId_ReportsError()
    {
        var project = CreateMinimalProject();
        project.Id = "";

        var report = _validator.ValidateTestProject(project);

        Assert.False(report.IsValid);
        Assert.Contains(report.Errors, e => e.Code == "VAL_001");
    }

    [Fact]
    public void ValidateTestProject_EmptyName_ReportsError()
    {
        var project = CreateMinimalProject();
        project.Name = "";

        var report = _validator.ValidateTestProject(project);

        Assert.False(report.IsValid);
        Assert.Contains(report.Errors, e => e.Code == "VAL_003");
    }

    [Fact]
    public void ValidateTestProject_ValidProject_PassesValidation()
    {
        var project = CreateMinimalProject();
        project.Steps = new List<ConfigTestStep>
        {
            new() { Id = "step-1", Name = "Step 1", Order = 1, Type = "serial", Command = "AT" }
        };

        var report = _validator.ValidateTestProject(project);

        Assert.True(report.IsValid);
        Assert.Empty(report.Errors);
    }

    #endregion

    #region ValidateSteps

    [Fact]
    public void ValidateTestProject_NullSteps_ReportsError()
    {
        var project = CreateMinimalProject();
        project.Steps = null;

        var report = _validator.ValidateTestProject(project);

        Assert.False(report.IsValid);
        Assert.Contains(report.Errors, e => e.Code == "VAL_010");
    }

    [Fact]
    public void ValidateTestProject_EmptyStepId_ReportsError()
    {
        var project = CreateMinimalProject();
        project.Steps = new List<ConfigTestStep>
        {
            new() { Id = "", Name = "Step 1", Order = 1, Type = "serial", Command = "AT" }
        };

        var report = _validator.ValidateTestProject(project);

        Assert.Contains(report.Errors, e => e.Message.Contains("ID"));
    }

    [Fact]
    public void ValidateTestProject_InvalidStepType_ReportsWarning()
    {
        var project = CreateMinimalProject();
        project.Steps = new List<ConfigTestStep>
        {
            new() { Id = "step-1", Name = "Step 1", Order = 1, Type = "invalid_type", Command = "AT" }
        };

        var report = _validator.ValidateTestProject(project);

        Assert.Contains(report.Warnings, w => w.Code == "VAL_W003");
    }

    [Fact]
    public void ValidateTestProject_DuplicateStepIds_ReportsError()
    {
        var project = CreateMinimalProject();
        project.Steps = new List<ConfigTestStep>
        {
            new() { Id = "step-1", Name = "Step 1", Order = 1, Type = "serial", Command = "AT" },
            new() { Id = "step-1", Name = "Step 2", Order = 2, Type = "serial", Command = "AT+GMR" }
        };

        var report = _validator.ValidateTestProject(project);

        Assert.Contains(report.Errors, e => e.Message.Contains("重复"));
    }

    #endregion

    #region ValidateExpectedValues

    [Fact]
    public void ValidateTestProject_InvalidRegexExpected_ReportsError()
    {
        var project = CreateMinimalProject();
        project.Steps = new List<ConfigTestStep>
        {
            new() { Id = "step-1", Name = "Step 1", Order = 1, Type = "serial", Command = "AT", Expected = "regex:[invalid(" }
        };

        var report = _validator.ValidateTestProject(project);

        Assert.Contains(report.Errors, e => e.Code == "VAL_020");
    }

    [Fact]
    public void ValidateTestProject_ValidRegexExpected_NoError()
    {
        var project = CreateMinimalProject();
        project.Steps = new List<ConfigTestStep>
        {
            new() { Id = "step-1", Name = "Step 1", Order = 1, Type = "serial", Command = "AT", Expected = @"regex:OK\s+" }
        };

        var report = _validator.ValidateTestProject(project);

        Assert.DoesNotContain(report.Errors, e => e.Code == "VAL_020");
    }

    #endregion

    #region ValidateVariableTemplates

    [Fact]
    public void ValidateTestProject_UndefinedVariable_ReportsError()
    {
        var project = CreateMinimalProject();
        project.Steps = new List<ConfigTestStep>
        {
            new() { Id = "step-1", Name = "Step 1", Order = 1, Type = "cmd", Command = "echo {{undefined_var}}" }
        };

        var report = _validator.ValidateTestProject(project);

        Assert.Contains(report.Errors, e => e.Code == "VAL_030");
    }

    [Fact]
    public void ValidateTestProject_DefinedVariable_NoError()
    {
        var project = CreateMinimalProject();
        project.Steps = new List<ConfigTestStep>
        {
            new() { Id = "step-1", Name = "Step 1", Order = 1, Type = "cmd", Command = "get version", StoreResultAs = "fw_version" },
            new() { Id = "step-2", Name = "Step 2", Order = 2, Type = "cmd", Command = "verify {{fw_version}}" }
        };

        var report = _validator.ValidateTestProject(project);

        Assert.DoesNotContain(report.Errors, e => e.Code == "VAL_030");
    }

    [Fact]
    public void ValidateTestProject_VariableUsedBeforeProduced_ReportsError()
    {
        var project = CreateMinimalProject();
        project.Steps = new List<ConfigTestStep>
        {
            new() { Id = "step-1", Name = "Step 1", Order = 1, Type = "cmd", Command = "verify {{fw_version}}" },
            new() { Id = "step-2", Name = "Step 2", Order = 2, Type = "cmd", Command = "get version", StoreResultAs = "fw_version" }
        };

        var report = _validator.ValidateTestProject(project);

        Assert.Contains(report.Errors, e => e.Code == "VAL_030");
    }

    #endregion

    #region ValidateConditionExpressions

    [Fact]
    public void ValidateTestProject_ValidExistsCondition_NoError()
    {
        var project = CreateMinimalProject();
        project.Steps = new List<ConfigTestStep>
        {
            new() { Id = "step-1", Name = "Step 1", Order = 1, Type = "cmd", Command = "get version", StoreResultAs = "fw_version" },
            new() { Id = "step-2", Name = "Step 2", Order = 2, Type = "cmd", Command = "next", ConditionExpression = "exists:fw_version" }
        };

        var report = _validator.ValidateTestProject(project);

        Assert.DoesNotContain(report.Errors, e => e.Code == "VAL_031" || e.Code == "VAL_033");
    }

    [Fact]
    public void ValidateTestProject_InvalidConditionPrefix_ReportsError()
    {
        var project = CreateMinimalProject();
        project.Steps = new List<ConfigTestStep>
        {
            new() { Id = "step-1", Name = "Step 1", Order = 1, Type = "cmd", Command = "test", ConditionExpression = "badprefix:value" }
        };

        var report = _validator.ValidateTestProject(project);

        Assert.Contains(report.Errors, e => e.Code == "VAL_033");
    }

    [Fact]
    public void ValidateTestProject_EmptyExistsCondition_ReportsError()
    {
        var project = CreateMinimalProject();
        project.Steps = new List<ConfigTestStep>
        {
            new() { Id = "step-1", Name = "Step 1", Order = 1, Type = "cmd", Command = "test", ConditionExpression = "exists:" }
        };

        var report = _validator.ValidateTestProject(project);

        Assert.Contains(report.Errors, e => e.Code == "VAL_031");
    }

    #endregion

    #region ValidateStoreResultDependencies

    [Fact]
    public void ValidateTestProject_DuplicateStoreResultAs_ReportsWarning()
    {
        var project = CreateMinimalProject();
        project.Steps = new List<ConfigTestStep>
        {
            new() { Id = "step-1", Name = "Step 1", Order = 1, Type = "cmd", Command = "get v1", StoreResultAs = "version" },
            new() { Id = "step-2", Name = "Step 2", Order = 2, Type = "cmd", Command = "get v2", StoreResultAs = "version" }
        };

        var report = _validator.ValidateTestProject(project);

        Assert.Contains(report.Warnings, w => w.Code == "VAL_W032");
    }

    #endregion

    #region ValidateValidationRules

    [Fact]
    public void ValidateTestProject_NumericRangeMinGreaterThanMax_ReportsError()
    {
        var project = CreateMinimalProject();
        project.Steps = new List<ConfigTestStep>
        {
            new()
            {
                Id = "step-1", Name = "Step 1", Order = 1, Type = "cmd", Command = "measure",
                ValidationRules = new Dictionary<string, object>
                {
                    ["NumericRange"] = new Dictionary<string, object> { ["Min"] = 10.0, ["Max"] = 5.0 }
                }
            }
        };

        var report = _validator.ValidateTestProject(project);

        Assert.Contains(report.Errors, e => e.Code == "VAL_041");
    }

    #endregion

    #region ValidateChannelTypeConsistency

    [Fact]
    public void ValidateTestProject_MismatchedTypeAndChannel_ReportsWarning()
    {
        var project = CreateMinimalProject();
        project.Steps = new List<ConfigTestStep>
        {
            new() { Id = "step-1", Name = "Step 1", Order = 1, Type = "serial", Channel = "http", Command = "AT" }
        };

        var report = _validator.ValidateTestProject(project);

        Assert.Contains(report.Warnings, w => w.Code == "VAL_W040");
    }

    [Fact]
    public void ValidateTestProject_MatchedTypeAndChannel_NoWarning()
    {
        var project = CreateMinimalProject();
        project.Steps = new List<ConfigTestStep>
        {
            new() { Id = "step-1", Name = "Step 1", Order = 1, Type = "serial", Channel = "COM3", Command = "AT" }
        };

        var report = _validator.ValidateTestProject(project);

        Assert.DoesNotContain(report.Warnings, w => w.Code == "VAL_W040");
    }

    #endregion

    #region GenerateReportSummary

    [Fact]
    public void GenerateReportSummary_ValidReport_ContainsExpectedSections()
    {
        var project = CreateMinimalProject();
        project.Steps = new List<ConfigTestStep>
        {
            new() { Id = "step-1", Name = "Step 1", Order = 1, Type = "serial", Command = "AT" }
        };

        var report = _validator.ValidateTestProject(project);
        var summary = _validator.GenerateReportSummary(report);

        Assert.Contains("验证报告", summary);
        Assert.Contains(project.Name, summary);
    }

    #endregion

    private static ConfigTestProject CreateMinimalProject()
    {
        return new ConfigTestProject
        {
            Id = "test-project-001",
            Name = "Test Project",
            Description = "Unit test project",
            Enabled = true,
            Steps = new List<ConfigTestStep>()
        };
    }
}
