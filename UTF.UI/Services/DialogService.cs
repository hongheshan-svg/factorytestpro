using System.Windows;
using Microsoft.Win32;

namespace UTF.UI.Services;

/// <summary>
/// <see cref="IDialogService"/> 的默认实现。封装 <see cref="MessageBox"/> 与
/// <see cref="OpenFileDialog"/> / <see cref="SaveFileDialog"/> 调用。
/// 该实现无状态，注册为单例即可。
/// </summary>
public sealed class DialogService : IDialogService
{
    /// <inheritdoc />
    public void ShowInformation(string message, string title = "信息")
        => MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);

    /// <inheritdoc />
    public void ShowWarning(string message, string title = "警告")
        => MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);

    /// <inheritdoc />
    public void ShowError(string message, string title = "错误")
        => MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);

    /// <inheritdoc />
    public bool ShowConfirmation(string message, string title = "确认")
        => MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    /// <inheritdoc />
    public string? ShowOpenFileDialog(string title, string filter = "JSON 配置文件|*.json|所有文件|*.*")
    {
        var dialog = new OpenFileDialog
        {
            Title = title,
            Filter = filter
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    /// <inheritdoc />
    public string? ShowSaveFileDialog(string title, string filter, string defaultExtension, string? defaultFileName = null)
    {
        var dialog = new SaveFileDialog
        {
            Title = title,
            Filter = filter,
            DefaultExt = defaultExtension
        };
        if (!string.IsNullOrWhiteSpace(defaultFileName))
        {
            dialog.FileName = defaultFileName;
        }
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
