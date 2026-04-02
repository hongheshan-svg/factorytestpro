using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using UTF.Core;

namespace UTF.UI;

/// <summary>
/// DUT测试卡片控件
/// </summary>
public partial class DUTTestCard : UserControl
{
    private DUTTestInfo? _dutTestInfo;

    public DUTTestCard()
    {
        InitializeComponent();
        
        // 添加双击事件处理
        MouseDoubleClick += DUTTestCard_MouseDoubleClick;
        Cursor = System.Windows.Input.Cursors.Hand;
        
        // 添加工具提示
        ToolTip = "双击查看详细信息";
    }

    /// <summary>
    /// 更新DUT信息
    /// </summary>
    public void UpdateDUTInfo(DUTTestInfo dutInfo)
    {
        _dutTestInfo = dutInfo;
        
        DutIdText.Text = dutInfo.DutId;
        DutNameText.Text = dutInfo.DutName;
        CurrentStepText.Text = dutInfo.CurrentStep;
        ProgressText.Text = dutInfo.Progress;
        DurationText.Text = dutInfo.DurationDisplay;
        StartTimeText.Text = dutInfo.StartTimeDisplay;
        
        // 更新进度条
        if (ParseProgress(dutInfo.Progress, out int current, out int total))
        {
            TestProgressBar.Value = total > 0 ? (double)current / total * 100 : 0;
        }
        
        // 更新状态
        UpdateStatus(dutInfo.Status.ToString());
        
        // 更新结果
        UpdateResult(dutInfo.Result.ToString());
    }

    /// <summary>
    /// 更新状态显示
    /// </summary>
    private void UpdateStatus(string status)
    {
        StatusText.Text = status;
        
        var (background, foreground) = status.ToLower() switch
        {
            "测试中" or "running" => ("#FF9800", "White"),
            "完成" or "completed" => ("#4CAF50", "White"),
            "故障" or "error" => ("#F44336", "White"),
            "等待" or "waiting" => ("#9E9E9E", "White"),
            "已连接" or "connected" => ("#2196F3", "White"),
            _ => ("#9E9E9E", "White")
        };
        
        StatusBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(background));
        StatusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(foreground));
    }

    /// <summary>
    /// 更新结果显示
    /// </summary>
    private void UpdateResult(string result)
    {
        ResultText.Text = result;
        
        var (background, foreground) = result.ToUpper() switch
        {
            "PASS" or "通过" or "成功" => ("#4CAF50", "White"),
            "FAIL" or "失败" or "错误" => ("#F44336", "White"),
            "执行中" or "RUNNING" => ("#FF9800", "White"),
            "等待" or "WAITING" => ("#9E9E9E", "White"),
            _ => ("#E0E0E0", "#757575")
        };
        
        ResultBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(background));
        ResultText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(foreground));
    }

    /// <summary>
    /// 解析进度字符串
    /// </summary>
    private bool ParseProgress(string progress, out int current, out int total)
    {
        current = 0;
        total = 0;
        
        if (string.IsNullOrEmpty(progress) || !progress.Contains('/'))
            return false;
        
        var parts = progress.Split('/');
        if (parts.Length != 2)
            return false;
        
        return int.TryParse(parts[0], out current) && int.TryParse(parts[1], out total);
    }
    
    /// <summary>
    /// 双击事件处理 - 显示DUT详细信息
    /// </summary>
    private void DUTTestCard_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_dutTestInfo != null)
        {
            ShowDUTDetails();
        }
    }
    
    /// <summary>
    /// 显示DUT详细信息窗口
    /// </summary>
    private void ShowDUTDetails()
    {
        if (_dutTestInfo != null)
        {
            var detailWindow = new DUTDetailWindow(_dutTestInfo);
            detailWindow.Show();
        }
    }
}
