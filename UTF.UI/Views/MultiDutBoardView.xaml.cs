using System.Windows.Controls;

namespace UTF.UI.Views;

/// <summary>
/// Default multi-DUT DataGrid monitor board (WorkbenchMode = MultiDutBoard).
/// </summary>
public partial class MultiDutBoardView : UserControl
{
    public MultiDutBoardView()
    {
        InitializeComponent();
    }

    /// <summary>Primary DUT monitor grid used by <see cref="Services.DUTMonitorManager"/> for dynamic columns.</summary>
    public DataGrid DutDataGrid => MainDUTListDataGrid;
}
