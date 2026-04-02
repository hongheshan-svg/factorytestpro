namespace UTF.Core;

/// <summary>
/// 测试验证器接口 - 负责结果验证
/// </summary>
public interface ITestValidator
{
    TestValidationResult Validate(string actual, string expected, string? rule = null);
}

public record TestValidationResult(bool IsValid, string? ErrorMessage = null);
