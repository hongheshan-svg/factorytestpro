using System.Linq;
using UTF.Configuration.Abstractions;
using UTF.Configuration.Models;

namespace UTF.Configuration.Validators;

public class TestConfigValidator : IConfigurationValidator<TestConfig>
{
    public ConfigValidationResult Validate(TestConfig config)
    {
        var errors = new System.Collections.Generic.List<string>();

        if (string.IsNullOrWhiteSpace(config.ProjectName))
            errors.Add("项目名称不能为空");

        var stepIds = config.Steps.Select(s => s.Id).ToList();
        if (stepIds.Count != stepIds.Distinct().Count())
            errors.Add("步骤ID存在重复");

        foreach (var step in config.Steps)
        {
            if (string.IsNullOrWhiteSpace(step.Name))
                errors.Add($"步骤 {step.Id} 名称不能为空");
        }

        return errors.Any()
            ? ConfigValidationResult.Fail(errors.ToArray())
            : ConfigValidationResult.Success();
    }
}
