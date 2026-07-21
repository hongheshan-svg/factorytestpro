using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using UTF.Plugin.Abstractions;

namespace UTF.UI.ViewModels;

/// <summary>
/// One dynamic step-parameter editor row bound to <c>step.Parameters[name]</c>.
/// </summary>
public partial class StepParameterFieldViewModel : ObservableObject
{
    private readonly Dictionary<string, object> _parameters;
    private readonly string _name;
    private bool _suppressWrite;

    public StepParameterFieldViewModel(
        PluginParameterSchemaItem schema,
        Dictionary<string, object> parameters)
    {
        ArgumentNullException.ThrowIfNull(schema);
        _parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
        _name = string.IsNullOrWhiteSpace(schema.Name)
            ? throw new ArgumentException("Schema item Name is required.", nameof(schema))
            : schema.Name.Trim();

        Label = string.IsNullOrWhiteSpace(schema.Label) ? _name : schema.Label!;
        FieldType = NormalizeType(schema.Type);
        IsRequired = schema.Required;
        EnumValues = schema.EnumValues?
            .Where(v => v is not null)
            .Select(v => v!)
            .ToArray();

        _suppressWrite = true;
        try
        {
            var initial = ResolveInitialValue(schema);
            if (IsBool)
            {
                BoolValue = ParseBool(initial);
            }
            else
            {
                StringValue = initial ?? string.Empty;
            }
        }
        finally
        {
            _suppressWrite = false;
        }
    }

    public string Name => _name;

    public string Label { get; }

    /// <summary>Normalized type: string | int | bool | double.</summary>
    public string FieldType { get; }

    public bool IsRequired { get; }

    public string[]? EnumValues { get; }

    public bool IsBool => string.Equals(FieldType, "bool", StringComparison.OrdinalIgnoreCase);

    public bool IsEnum => EnumValues is { Length: > 0 } && !IsBool;

    public bool IsText => !IsBool && !IsEnum;

    [ObservableProperty]
    private string _stringValue = string.Empty;

    [ObservableProperty]
    private bool _boolValue;

    partial void OnStringValueChanged(string value)
    {
        if (_suppressWrite || IsBool)
        {
            return;
        }

        WriteString(value);
    }

    partial void OnBoolValueChanged(bool value)
    {
        if (_suppressWrite || !IsBool)
        {
            return;
        }

        _parameters[_name] = value;
    }

    /// <summary>Flush current editor value into the step parameters dictionary.</summary>
    public void Commit()
    {
        if (IsBool)
        {
            _parameters[_name] = BoolValue;
        }
        else
        {
            WriteString(StringValue);
        }
    }

    private void WriteString(string? raw)
    {
        var text = raw ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text) && !IsRequired)
        {
            // Keep empty optional values out of the dictionary so they don't override plugin settings.
            _parameters.Remove(_name);
            return;
        }

        switch (FieldType.ToLowerInvariant())
        {
            case "int":
                if (int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var i))
                {
                    _parameters[_name] = i;
                }
                else
                {
                    _parameters[_name] = text;
                }
                break;
            case "double":
                if (double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
                {
                    _parameters[_name] = d;
                }
                else
                {
                    _parameters[_name] = text;
                }
                break;
            default:
                _parameters[_name] = text;
                break;
        }
    }

    private string? ResolveInitialValue(PluginParameterSchemaItem schema)
    {
        if (_parameters.TryGetValue(_name, out var existing) && existing is not null)
        {
            return ConvertToDisplayString(existing);
        }

        return schema.Default;
    }

    private static string ConvertToDisplayString(object value)
    {
        return value switch
        {
            bool b => b ? "true" : "false",
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty,
            _ => value.ToString() ?? string.Empty
        };
    }

    private static bool ParseBool(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        if (bool.TryParse(text, out var b))
        {
            return b;
        }

        if (text is "1" or "yes" or "Yes" or "YES")
        {
            return true;
        }

        return false;
    }

    private static string NormalizeType(string? type)
    {
        var t = (type ?? "string").Trim().ToLowerInvariant();
        return t switch
        {
            "int" or "integer" => "int",
            "bool" or "boolean" => "bool",
            "double" or "float" or "number" => "double",
            _ => "string"
        };
    }
}
