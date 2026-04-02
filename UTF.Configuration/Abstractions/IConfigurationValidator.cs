namespace UTF.Configuration.Abstractions;

/// <summary>
/// 配置验证器接口
/// </summary>
public interface IConfigurationValidator<T> where T : class
{
    ConfigValidationResult Validate(T config);
}

public record ConfigValidationResult(bool IsValid, string[] Errors)
{
    public static ConfigValidationResult Success() => new(true, Array.Empty<string>());
    public static ConfigValidationResult Fail(params string[] errors) => new(false, errors);
}
