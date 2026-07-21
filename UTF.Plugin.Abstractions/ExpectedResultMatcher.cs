using System.Text.RegularExpressions;

namespace UTF.Plugin.Abstractions;

/// <summary>
/// 期望结果匹配器 - 统一解析 <c>contains:</c>/<c>equals:</c>/<c>regex:</c>/
/// <c>notcontains:</c> 前缀及裸文本匹配。
/// </summary>
/// <remarks>
/// 本类型同时存在于 <c>UTF.Core.Validation.ExpectedResultMatcher</c>（供宿主侧使用）
/// 与 <c>UTF.Plugin.Abstractions.ExpectedResultMatcher</c>（供插件侧使用，避免插件反向依赖
/// UTF.Core 造成循环引用）。二者语义保持一致。如果 <c>UTF.Plugin.Abstractions</c> 未来
/// 能直接引用 <c>UTF.Core</c> 而不产生循环，应统一改用 <c>UTF.Core.Validation</c> 中的实现。
/// </remarks>
public static class ExpectedResultMatcher
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(2);

    /// <summary>
    /// 判断 <paramref name="actual"/> 是否满足 <paramref name="expected"/> 表达式。
    /// </summary>
    /// <param name="expected">期望表达式，支持前缀 <c>contains:</c> / <c>equals:</c> /
    /// <c>regex:</c> / <c>notcontains:</c>；无前缀时按包含语义匹配。空表达式视为通过。</param>
    /// <param name="actual">实际响应文本。</param>
    /// <returns>匹配返回 true，否则 false。</returns>
    public static bool Match(string expected, string actual)
    {
        var text = actual ?? string.Empty;
        var expression = (expected ?? string.Empty).Trim();

        if (string.IsNullOrEmpty(expression))
        {
            return true;
        }

        if (expression.StartsWith("notcontains:", StringComparison.OrdinalIgnoreCase))
        {
            var needle = expression["notcontains:".Length..];
            return !text.Contains(needle, StringComparison.OrdinalIgnoreCase);
        }

        if (expression.StartsWith("contains:", StringComparison.OrdinalIgnoreCase))
        {
            var needle = expression["contains:".Length..];
            return text.Contains(needle, StringComparison.OrdinalIgnoreCase);
        }

        if (expression.StartsWith("equals:", StringComparison.OrdinalIgnoreCase))
        {
            var want = expression["equals:".Length..];
            return string.Equals(text.Trim(), want.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        if (expression.StartsWith("regex:", StringComparison.OrdinalIgnoreCase))
        {
            var pattern = expression["regex:".Length..];
            try
            {
                return Regex.IsMatch(text, pattern,
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, RegexTimeout);
            }
            catch (Exception ex) when (ex is ArgumentException or RegexMatchTimeoutException)
            {
                return false;
            }
        }

        // 裸文本：按包含语义匹配
        return text.Contains(expression, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 判断 <paramref name="actual"/> 是否满足 <paramref name="expected"/> 表达式，并输出失败原因。
    /// </summary>
    public static bool Match(string expected, string actual, out string reason)
    {
        reason = string.Empty;
        var ok = Match(expected, actual);
        if (!ok)
        {
            reason = BuildReason(expected, actual);
        }

        return ok;
    }

    private static string BuildReason(string expected, string actual)
    {
        var expression = (expected ?? string.Empty).Trim();

        if (expression.StartsWith("notcontains:", StringComparison.OrdinalIgnoreCase))
        {
            return $"响应意外包含排除内容: {expression["notcontains:".Length..]}";
        }

        if (expression.StartsWith("contains:", StringComparison.OrdinalIgnoreCase))
        {
            return $"响应不包含预期内容: {expression["contains:".Length..]}";
        }

        if (expression.StartsWith("equals:", StringComparison.OrdinalIgnoreCase))
        {
            return $"响应与预期不一致，预期: {expression["equals:".Length..]}";
        }

        if (expression.StartsWith("regex:", StringComparison.OrdinalIgnoreCase))
        {
            return $"响应不匹配正则: {expression["regex:".Length..]}";
        }

        return $"响应不包含预期文本: {expression}";
    }
}
