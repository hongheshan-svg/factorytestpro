namespace UTF.UI.Services;

/// <summary>
/// 对话框服务抽象。封装消息框与文件对话框的展示逻辑，使视图模型
/// 在不直接引用 <c>System.Windows.MessageBox</c> / <c>Microsoft.Win32</c>
/// 的前提下完成用户交互（信息提示、确认、文件选择）。
/// </summary>
public interface IDialogService
{
    /// <summary>展示信息提示框。</summary>
    /// <param name="message">消息正文。</param>
    /// <param name="title">窗口标题，默认“信息”。</param>
    void ShowInformation(string message, string title = "信息");

    /// <summary>展示警告提示框。</summary>
    /// <param name="message">消息正文。</param>
    /// <param name="title">窗口标题，默认“警告”。</param>
    void ShowWarning(string message, string title = "警告");

    /// <summary>展示错误提示框。</summary>
    /// <param name="message">消息正文。</param>
    /// <param name="title">窗口标题，默认“错误”。</param>
    void ShowError(string message, string title = "错误");

    /// <summary>展示确认提示框，返回用户是否确认。</summary>
    /// <param name="message">消息正文。</param>
    /// <param name="title">窗口标题，默认“确认”。</param>
    /// <returns>用户点击“是”返回 <c>true</c>，否则 <c>false</c>。</returns>
    bool ShowConfirmation(string message, string title = "确认");

    /// <summary>展示打开文件对话框，返回所选文件路径；用户取消则返回 <c>null</c>。</summary>
    /// <param name="title">对话框标题。</param>
    /// <param name="filter">文件过滤器，默认“JSON 配置文件|*.json|所有文件|*.*”。</param>
    /// <returns>所选文件的完整路径；取消则 <c>null</c>。</returns>
    string? ShowOpenFileDialog(string title, string filter = "JSON 配置文件|*.json|所有文件|*.*");

    /// <summary>展示保存文件对话框，返回目标文件路径；用户取消则返回 <c>null</c>。</summary>
    /// <param name="title">对话框标题。</param>
    /// <param name="filter">文件过滤器，如“HTML 报告|*.html|CSV 报告|*.csv”。</param>
    /// <param name="defaultExtension">默认扩展名（不含点），如“html”。</param>
    /// <param name="defaultFileName">建议文件名（可含扩展名）。</param>
    /// <returns>目标文件完整路径；取消则 <c>null</c>。</returns>
    string? ShowSaveFileDialog(string title, string filter, string defaultExtension, string? defaultFileName = null);
}
