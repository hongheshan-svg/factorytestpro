using System;
using UTF.Plugin.Abstractions;

namespace UTF.Core.Validation;

/// <summary>
/// 期望结果匹配器 - 统一解析 expected 模式并与实际输出比较。
/// 支持前缀: contains:（默认）、equals:、regex:、notcontains:，以及无前缀的字面量（默认 contains 语义）。
/// </summary>
/// <remarks>
/// 规范实现位于 <see cref="UTF.Plugin.Abstractions.ExpectedResultMatcher"/>（最底层依赖，所有项目均可引用）。
/// 本类型为宿主侧（<c>UTF.Core</c>）的薄包装，转发到该实现以保证全代码库单一真相。
/// </remarks>
public static class ExpectedResultMatcher
{
    /// <summary>
    /// 判断 <paramref name="actual"/> 是否匹配 <paramref name="expected"/> 模式。
    /// </summary>
    /// <param name="expected">期望模式，可带前缀 contains:/equals:/regex:/notcontains:</param>
    /// <param name="actual">实际输出；null 视为空字符串</param>
    /// <returns>是否匹配</returns>
    public static bool Match(string expected, string actual)
        => UTF.Plugin.Abstractions.ExpectedResultMatcher.Match(expected, actual);

    /// <summary>
    /// 判断 <paramref name="actual"/> 是否匹配 <paramref name="expected"/> 模式，并输出失败原因。
    /// </summary>
    public static bool Match(string expected, string actual, out string reason)
        => UTF.Plugin.Abstractions.ExpectedResultMatcher.Match(expected, actual, out reason);
}
