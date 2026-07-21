using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace UTF.UI.Models;

/// <summary>
/// Well-known workbench mode identifiers (matches <c>UiProfile.Mode</c>).
/// </summary>
public static class WorkbenchModes
{
    public const string MultiDutBoard = "MultiDutBoard";
    public const string SingleStation = "SingleStation";
    public const string ScanToTest = "ScanToTest";
    public const string InstrumentBench = "InstrumentBench";

    public static readonly string[] All =
    {
        MultiDutBoard,
        SingleStation,
        ScanToTest,
        InstrumentBench
    };

    public static string Normalize(string? mode)
    {
        if (string.IsNullOrWhiteSpace(mode))
        {
            return MultiDutBoard;
        }

        foreach (var known in All)
        {
            if (string.Equals(known, mode.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return known;
            }
        }

        return MultiDutBoard;
    }

    public static string DisplayName(string mode) => Normalize(mode) switch
    {
        SingleStation => "单工位",
        ScanToTest => "扫码测试",
        InstrumentBench => "仪器台",
        _ => "多DUT看板"
    };
}

/// <summary>
/// Lightweight instrument / communication endpoint row for InstrumentBench.
/// </summary>
public sealed class InstrumentEndpointItem : INotifyPropertyChanged
{
    private string _kind = "";
    private string _address = "";
    private string _status = "就绪";

    public string Kind
    {
        get => _kind;
        set { _kind = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayName)); }
    }

    public string Address
    {
        get => _address;
        set { _address = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayName)); }
    }

    public string Status
    {
        get => _status;
        set { _status = value; OnPropertyChanged(); }
    }

    public string DisplayName => string.IsNullOrEmpty(Kind) ? Address : $"{Kind}: {Address}";

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
