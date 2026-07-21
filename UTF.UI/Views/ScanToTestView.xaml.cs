using System.Windows.Controls;

namespace UTF.UI.Views;

/// <summary>
/// Barcode / SN scan-to-start panel (WorkbenchMode = ScanToTest).
/// </summary>
public partial class ScanToTestView : UserControl
{
    public ScanToTestView()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            try
            {
                BarcodeTextBox.Focus();
                System.Windows.Input.Keyboard.Focus(BarcodeTextBox);
            }
            catch
            {
                // Design-time / non-interactive host: ignore.
            }
        };
    }

    /// <summary>Focus the barcode entry field (e.g. after mode switch).</summary>
    public void FocusBarcode()
    {
        BarcodeTextBox.Focus();
        System.Windows.Input.Keyboard.Focus(BarcodeTextBox);
    }
}
