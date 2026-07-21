using System;
using System.Globalization;
using System.Windows;
using UTF.UI.Converters;
using Xunit;

namespace UTF.UI.Tests;

/// <summary>
/// Unit tests for <see cref="NullToVisibilityConverter"/> and
/// <see cref="StringToNullableIntConverter"/>. Both converters are pure
/// (no WPF dispatcher dependency) and can be exercised directly.
/// </summary>
public class ConvertersTests
{
    private static readonly CultureInfo InvariantCulture = CultureInfo.InvariantCulture;

    [Fact]
    [Trait("Category", "Unit")]
    public void NullToVisibilityConverter_Convert_NullValue_ReturnsCollapsed()
    {
        var converter = new NullToVisibilityConverter();

        var result = converter.Convert(null, typeof(Visibility), null!, InvariantCulture);

        Assert.Equal(Visibility.Collapsed, result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void NullToVisibilityConverter_Convert_NonNullValue_ReturnsVisible()
    {
        var converter = new NullToVisibilityConverter();

        var result = converter.Convert("non-null", typeof(Visibility), null!, InvariantCulture);

        Assert.Equal(Visibility.Visible, result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void StringToNullableIntConverter_ConvertBack_ValidInt_ReturnsParsedInt()
    {
        var converter = new StringToNullableIntConverter();

        var result = converter.ConvertBack("42", typeof(int?), null!, InvariantCulture);

        Assert.Equal(42, result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void StringToNullableIntConverter_ConvertBack_EmptyString_ReturnsNull()
    {
        var converter = new StringToNullableIntConverter();

        var result = converter.ConvertBack(string.Empty, typeof(int?), null!, InvariantCulture);

        Assert.Null(result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void StringToNullableIntConverter_ConvertBack_GarbageString_ReturnsNull()
    {
        var converter = new StringToNullableIntConverter();

        var result = converter.ConvertBack("not-a-number", typeof(int?), null!, InvariantCulture);

        Assert.Null(result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void StringToNullableIntConverter_Convert_NullableInt_ReturnsString()
    {
        var converter = new StringToNullableIntConverter();

        var result = converter.Convert(7, typeof(string), null!, InvariantCulture);

        Assert.Equal("7", result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void StringToNullableIntConverter_Convert_NullValue_ReturnsEmptyString()
    {
        var converter = new StringToNullableIntConverter();

        var result = converter.Convert(null!, typeof(string), null!, InvariantCulture);

        Assert.Equal(string.Empty, result);
    }
}
