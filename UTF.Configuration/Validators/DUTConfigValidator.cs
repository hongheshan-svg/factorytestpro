using System.Linq;
using UTF.Configuration.Abstractions;
using UTF.Configuration.Models;

namespace UTF.Configuration.Validators;

public class DUTConfigValidator : IConfigurationValidator<DUTConfig>
{
    public ConfigValidationResult Validate(DUTConfig config)
    {
        var errors = new System.Collections.Generic.List<string>();

        if (config.MaxConcurrent <= 0 || config.MaxConcurrent > 64)
            errors.Add("并发数必须在1-64之间");

        if (string.IsNullOrWhiteSpace(config.ProductName))
            errors.Add("产品名称不能为空");

        return errors.Any()
            ? ConfigValidationResult.Fail(errors.ToArray())
            : ConfigValidationResult.Success();
    }
}
