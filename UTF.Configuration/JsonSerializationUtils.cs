using System;
using System.Text.Json;
using System.Text;
using System.Collections.Generic;
using System.Linq;

namespace UTF.Configuration;

/// <summary>
/// JSON序列化工具类 - 提供格式转换功能
/// </summary>
public static class JsonSerializationUtils
{
    /// <summary>
    /// 将JSON转换为YAML格式
    /// </summary>
    public static string ConvertJsonToYaml(JsonElement json)
    {
        var sb = new StringBuilder();
        ConvertToYaml(json, sb, 0);
        return sb.ToString();
    }

    /// <summary>
    /// 将JSON转换为INI格式
    /// </summary>
    public static string ConvertJsonToIni(JsonElement json)
    {
        var sb = new StringBuilder();
        var flatDict = FlattenJsonObject(json, "");
        
        foreach (var kvp in flatDict)
        {
            sb.AppendLine($"{kvp.Key}={kvp.Value}");
        }
        
        return sb.ToString();
    }

    /// <summary>
    /// 将JSON转换为Properties格式
    /// </summary>
    public static string ConvertJsonToProperties(JsonElement json)
    {
        var sb = new StringBuilder();
        var flatDict = FlattenJsonObject(json, "");
        
        sb.AppendLine("# Generated configuration properties");
        sb.AppendLine($"# Generated at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();
        
        foreach (var kvp in flatDict.OrderBy(x => x.Key))
        {
            sb.AppendLine($"{kvp.Key}={kvp.Value}");
        }
        
        return sb.ToString();
    }

    private static void ConvertToYaml(JsonElement element, StringBuilder sb, int indent)
    {
        var indentStr = new string(' ', indent);
        
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    sb.Append($"{indentStr}{property.Name}:");
                    
                    if (property.Value.ValueKind == JsonValueKind.Object || 
                        property.Value.ValueKind == JsonValueKind.Array)
                    {
                        sb.AppendLine();
                        ConvertToYaml(property.Value, sb, indent + 2);
                    }
                    else
                    {
                        sb.Append(" ");
                        ConvertToYaml(property.Value, sb, 0);
                        sb.AppendLine();
                    }
                }
                break;
                
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    sb.Append($"{indentStr}- ");
                    ConvertToYaml(item, sb, indent + 2);
                    sb.AppendLine();
                }
                break;
                
            case JsonValueKind.String:
                sb.Append($"\"{element.GetString()}\"");
                break;
                
            case JsonValueKind.Number:
                sb.Append(element.GetRawText());
                break;
                
            case JsonValueKind.True:
            case JsonValueKind.False:
                sb.Append(element.GetBoolean().ToString().ToLower());
                break;
                
            case JsonValueKind.Null:
                sb.Append("null");
                break;
        }
    }

    private static Dictionary<string, string> FlattenJsonObject(JsonElement element, string prefix)
    {
        var result = new Dictionary<string, string>();
        
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    var key = string.IsNullOrEmpty(prefix) ? property.Name : $"{prefix}.{property.Name}";
                    var nested = FlattenJsonObject(property.Value, key);
                    
                    foreach (var kvp in nested)
                    {
                        result[kvp.Key] = kvp.Value;
                    }
                }
                break;
                
            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    var key = $"{prefix}[{index}]";
                    var nested = FlattenJsonObject(item, key);
                    
                    foreach (var kvp in nested)
                    {
                        result[kvp.Key] = kvp.Value;
                    }
                    index++;
                }
                break;
                
            default:
                result[prefix] = GetElementValue(element);
                break;
        }
        
        return result;
    }

    private static string GetElementValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? "",
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => "null",
            _ => element.GetRawText()
        };
    }

    /// <summary>
    /// 将YAML格式转换为JSON - 简化实现
    /// </summary>
    public static string ConvertYamlToJson(string yamlContent)
    {
        // 简化的YAML到JSON转换 - 实际应用中建议使用专业的YAML解析库
        // 这里提供基本实现以满足编译要求
        try
        {
            // 基本的键值对转换
            var lines = yamlContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var jsonBuilder = new StringBuilder("{");
            
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#"))
                    continue;
                    
                var colonIndex = trimmed.IndexOf(':');
                if (colonIndex > 0)
                {
                    var key = trimmed.Substring(0, colonIndex).Trim();
                    var value = trimmed.Substring(colonIndex + 1).Trim();
                    
                    if (jsonBuilder.Length > 1)
                        jsonBuilder.Append(",");
                        
                    jsonBuilder.Append($"\"{key}\":\"{value}\"");
                }
            }
            
            jsonBuilder.Append("}");
            return jsonBuilder.ToString();
        }
        catch
        {
            return "{}";
        }
    }

    /// <summary>
    /// 将INI格式转换为JSON
    /// </summary>
    public static string ConvertIniToJson(string iniContent)
    {
        try
        {
            var lines = iniContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var jsonBuilder = new StringBuilder("{");
            
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#") || trimmed.StartsWith(";"))
                    continue;
                    
                var equalIndex = trimmed.IndexOf('=');
                if (equalIndex > 0)
                {
                    var key = trimmed.Substring(0, equalIndex).Trim();
                    var value = trimmed.Substring(equalIndex + 1).Trim();
                    
                    if (jsonBuilder.Length > 1)
                        jsonBuilder.Append(",");
                        
                    jsonBuilder.Append($"\"{key}\":\"{value}\"");
                }
            }
            
            jsonBuilder.Append("}");
            return jsonBuilder.ToString();
        }
        catch
        {
            return "{}";
        }
    }

    /// <summary>
    /// 将Properties格式转换为JSON
    /// </summary>
    public static string ConvertPropertiesToJson(string propertiesContent)
    {
        return ConvertIniToJson(propertiesContent); // Properties格式与INI类似
    }
}
