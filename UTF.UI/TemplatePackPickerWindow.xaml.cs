using System;
using System.Windows;
using UTF.UI.ViewModels;

namespace UTF.UI;

/// <summary>
/// Modal dialog for browsing and applying product/process template packs.
/// </summary>
public partial class TemplatePackPickerWindow : Window
{
    private readonly TemplatePackPickerViewModel _viewModel;

    public TemplatePackPickerWindow(TemplatePackPickerViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = _viewModel;
        Loaded += OnLoaded;
        _viewModel.PackApplied += OnPackApplied;
        Closed += OnClosed;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            _viewModel.LoadPacks();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"加载模板目录失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnPackApplied(object? sender, EventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _viewModel.PackApplied -= OnPackApplied;
        Loaded -= OnLoaded;
        Closed -= OnClosed;
    }
}
