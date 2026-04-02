using System.Text.RegularExpressions;

namespace UTF.Core;

public class TestValidator : ITestValidator
{
    public TestValidationResult Validate(string actual, string expected, string? rule = null)
    {
        if (string.IsNullOrEmpty(expected))
            return new TestValidationResult(true);

        var prefix = rule ?? (expected.Contains(':') ? expected.Split(':')[0] : "contains");
        var value = expected.Contains(':') ? expected.Substring(expected.IndexOf(':') + 1) : expected;

        return prefix.ToLower() switch
        {
            "equals" => new TestValidationResult(actual == value, $"期望: {value}, 实际: {actual}"),
            "contains" => new TestValidationResult(actual.Contains(value), $"未包含: {value}"),
            "notcontains" => new TestValidationResult(!actual.Contains(value), $"不应包含: {value}"),
            "regex" => ValidateRegex(actual, value),
            _ => new TestValidationResult(actual.Contains(value))
        };
    }

    private TestValidationResult ValidateRegex(string actual, string pattern)
    {
        try
        {
            var match = Regex.IsMatch(actual, pattern);
            return new TestValidationResult(match, match ? null : $"不匹配正则: {pattern}");
        }
        catch
        {
            return new TestValidationResult(false, $"正则表达式无效: {pattern}");
        }
    }
}
