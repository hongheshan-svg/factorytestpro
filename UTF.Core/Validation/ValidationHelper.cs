using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace UTF.Core.Validation;

/// <summary>
/// 验证助手类
/// </summary>
public static class ValidationHelper
{
    /// <summary>验证字符串不为空</summary>
    public static ValidationResult ValidateNotEmpty(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return ValidationResult.Fail($"{fieldName} 不能为空");
        }
        return ValidationResult.Success();
    }

    /// <summary>验证字符串长度</summary>
    public static ValidationResult ValidateLength(string? value, string fieldName, int minLength, int maxLength)
    {
        if (value == null)
        {
            return ValidationResult.Fail($"{fieldName} 不能为null");
        }

        if (value.Length < minLength || value.Length > maxLength)
        {
            return ValidationResult.Fail($"{fieldName} 长度必须在 {minLength} 到 {maxLength} 之间");
        }

        return ValidationResult.Success();
    }

    /// <summary>验证数值范围</summary>
    public static ValidationResult ValidateRange<T>(T value, string fieldName, T min, T max) where T : IComparable<T>
    {
        if (value.CompareTo(min) < 0 || value.CompareTo(max) > 0)
        {
            return ValidationResult.Fail($"{fieldName} 必须在 {min} 到 {max} 之间");
        }
        return ValidationResult.Success();
    }

    /// <summary>验证正则表达式</summary>
    public static ValidationResult ValidateRegex(string? value, string fieldName, string pattern, string errorMessage)
    {
        if (value == null)
        {
            return ValidationResult.Fail($"{fieldName} 不能为null");
        }

        if (!Regex.IsMatch(value, pattern))
        {
            return ValidationResult.Fail(errorMessage);
        }

        return ValidationResult.Success();
    }

    /// <summary>验证邮箱地址</summary>
    public static ValidationResult ValidateEmail(string? email, string fieldName = "邮箱")
    {
        const string emailPattern = @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$";
        return ValidateRegex(email, fieldName, emailPattern, $"{fieldName} 格式不正确");
    }

    /// <summary>验证路径</summary>
    public static ValidationResult ValidatePath(string? path, string fieldName = "路径")
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return ValidationResult.Fail($"{fieldName} 不能为空");
        }

        try
        {
            var fullPath = System.IO.Path.GetFullPath(path);
            return ValidationResult.Success();
        }
        catch
        {
            return ValidationResult.Fail($"{fieldName} 格式不正确");
        }
    }

    /// <summary>验证文件存在</summary>
    public static ValidationResult ValidateFileExists(string? path, string fieldName = "文件")
    {
        var pathResult = ValidatePath(path, fieldName);
        if (!pathResult.IsValid)
            return pathResult;

        if (!System.IO.File.Exists(path))
        {
            return ValidationResult.Fail($"{fieldName} 不存在");
        }

        return ValidationResult.Success();
    }

    /// <summary>验证目录存在</summary>
    public static ValidationResult ValidateDirectoryExists(string? path, string fieldName = "目录")
    {
        var pathResult = ValidatePath(path, fieldName);
        if (!pathResult.IsValid)
            return pathResult;

        if (!System.IO.Directory.Exists(path))
        {
            return ValidationResult.Fail($"{fieldName} 不存在");
        }

        return ValidationResult.Success();
    }

    /// <summary>验证集合不为空</summary>
    public static ValidationResult ValidateNotEmpty<T>(IEnumerable<T>? collection, string fieldName)
    {
        if (collection == null || !collection.Any())
        {
            return ValidationResult.Fail($"{fieldName} 不能为空");
        }
        return ValidationResult.Success();
    }

    /// <summary>验证对象不为null</summary>
    public static ValidationResult ValidateNotNull<T>(T? value, string fieldName) where T : class
    {
        if (value == null)
        {
            return ValidationResult.Fail($"{fieldName} 不能为null");
        }
        return ValidationResult.Success();
    }

    /// <summary>组合多个验证结果</summary>
    public static ValidationResult Combine(params ValidationResult[] results)
    {
        var errors = results.Where(r => !r.IsValid).SelectMany(r => r.Errors).ToList();
        return errors.Any() ? ValidationResult.Fail(errors) : ValidationResult.Success();
    }
}

/// <summary>
/// 验证结果
/// </summary>
public sealed record ValidationResult
{
    /// <summary>是否验证成功</summary>
    public bool IsValid { get; init; }
    
    /// <summary>错误信息列表</summary>
    public List<string> Errors { get; init; } = new();

    /// <summary>创建成功结果</summary>
    public static ValidationResult Success() => new() { IsValid = true };

    /// <summary>创建失败结果</summary>
    public static ValidationResult Fail(string error) => new() { IsValid = false, Errors = new List<string> { error } };

    /// <summary>创建失败结果</summary>
    public static ValidationResult Fail(List<string> errors) => new() { IsValid = false, Errors = errors };

    /// <summary>获取第一个错误信息</summary>
    public string GetFirstError() => Errors.FirstOrDefault() ?? string.Empty;

    /// <summary>获取所有错误信息</summary>
    public string GetAllErrors() => string.Join("; ", Errors);
}

/// <summary>
/// 输入清理器
/// </summary>
public static class InputSanitizer
{
    /// <summary>清理HTML标签</summary>
    public static string RemoveHtmlTags(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        return Regex.Replace(input, "<.*?>", string.Empty);
    }

    /// <summary>清理SQL注入字符</summary>
    public static string RemoveSqlInjectionChars(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        // 移除常见的SQL注入字符
        input = input.Replace("'", "''");
        input = input.Replace("--", "");
        input = input.Replace(";", "");
        input = input.Replace("/*", "");
        input = input.Replace("*/", "");
        input = input.Replace("xp_", "");
        input = input.Replace("sp_", "");

        return input;
    }

    /// <summary>清理路径遍历字符</summary>
    public static string RemovePathTraversalChars(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        input = input.Replace("../", "");
        input = input.Replace("..\\", "");
        input = input.Replace("./", "");
        input = input.Replace(".\\", "");

        return input;
    }

    /// <summary>清理特殊字符</summary>
    public static string RemoveSpecialChars(string input, string allowedChars = "")
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        var pattern = $"[^a-zA-Z0-9{Regex.Escape(allowedChars)}]";
        return Regex.Replace(input, pattern, "");
    }

    /// <summary>限制字符串长度</summary>
    public static string LimitLength(string input, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        return input.Length > maxLength ? input.Substring(0, maxLength) : input;
    }
}

