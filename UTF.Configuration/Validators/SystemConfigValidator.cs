using System.Linq;
using UTF.Configuration.Abstractions;
using UTF.Configuration.Models;

namespace UTF.Configuration.Validators;

public class SystemConfigValidator : IConfigurationValidator<SystemConfig>
{
    public ConfigValidationResult Validate(SystemConfig config)
    {
        var errors = new System.Collections.Generic.List<string>();

        if (string.IsNullOrWhiteSpace(config.LogLevel))
            errors.Add("日志级别不能为空");

        if (string.IsNullOrWhiteSpace(config.ResultsPath))
            errors.Add("结果路径不能为空");

        if (string.IsNullOrWhiteSpace(config.DefaultLanguage))
            errors.Add("默认语言不能为空");

        return errors.Any()
            ? ConfigValidationResult.Fail(errors.ToArray())
            : ConfigValidationResult.Success();
    }
}
