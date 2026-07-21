using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using UTF.UI.Models;
using UTF.UI.Services;
using UTF.UI.ViewModels;

namespace UTF.UI
{
    public partial class ConfigurationCenterWindow : Window
    {
        private readonly ConfigurationCenterViewModel _viewModel;

        public ConfigurationCenterWindow(ConfigurationCenterViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            DataContext = _viewModel;
            Loaded += Window_Loaded;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ConfigurationCenterViewModel.ValidationStatus) ||
                e.PropertyName == nameof(ConfigurationCenterViewModel.ValidationStatusColor))
            {
                SyncValidationDisplay();
            }
            // SelectedStep 变化已由 XAML 双向绑定驱动详情面板各字段 + NullToVisibilityConverter
            // 自动控制面板可见性，不再需要 ShowStepDetail 介入。
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // MVVM: VM 在 LoadConfigAsync 中同步填充所有集合与选中值（串口/主机/步骤/枚举），
                // XAML 双向绑定自动反映到控件，无需代码后置手动 PopulateManualFields。
                await _viewModel.LoadConfigAsync();
                SyncValidationDisplay();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载配置失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ── 命名模板预览 ─────────────────────────────────────────────────────────

        private void NamingTemplate_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateNamingPreview();
        }

        private void UpdateNamingPreview()
        {
            if (NamingPreviewText == null) return;
            var productName = ProductNameText?.Text?.Trim();
            if (string.IsNullOrEmpty(productName)) productName = "产品";

            var nameTemplate = NamingTemplateText?.Text ?? "{TypeName}测试工位{Index}";
            var idTemplate = NamingIdTemplateText?.Text ?? "DUT-{Index}";

            var displayName = nameTemplate
                .Replace("{TypeName}", productName)
                .Replace("{Index}", "1");
            var idName = idTemplate.Replace("{Index}", "1");

            NamingPreviewText.Text = $"{idName} -> {displayName}";
        }

        // ── 保存 / 加载 / 验证 ──────────────────────────────────────────────────

        private async void SaveConfig_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // MVVM: 所有字段（含原手动同步的 ComboBox/ListBox/DataGrid）现由 VM 双向绑定维护；
                // SaveCommand 内部会调用 SyncToConfig 写回 Config 后持久化。
                await _viewModel.SaveCommand.ExecuteAsync(null);
                SyncValidationDisplay();
                DialogResult = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ReloadConfig_Click(object sender, RoutedEventArgs e)
        {
            await _viewModel.ReloadCommand.ExecuteAsync(null);
            SyncValidationDisplay();
        }

        private void ValidateConfig_Click(object sender, RoutedEventArgs e)
        {
            // P4-27: collect the current UI fields into Config via VM, then delegate validation to the
            // IConfigurationAdapter (single source of truth - supports contains:/equals:/regex:/notcontains:).
            var errors = _viewModel.Validate();
            SyncValidationDisplay();

            if (errors != null && errors.Any())
            {
                MessageBox.Show(string.Join("\n", errors), "验证失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// 将 VM 的 ValidationStatus / ValidationStatusColor 同步到 ValidationStatusText 控件。
        /// </summary>
        private void SyncValidationDisplay()
        {
            if (ValidationStatusText == null) return;
            ValidationStatusText.Text = _viewModel.ValidationStatus;
            try
            {
                var brush = new System.Windows.Media.BrushConverter().ConvertFromString(_viewModel.ValidationStatusColor ?? string.Empty) as System.Windows.Media.Brush;
                ValidationStatusText.Foreground = brush ?? System.Windows.Media.Brushes.Green;
            }
            catch
            {
                ValidationStatusText.Foreground = System.Windows.Media.Brushes.Green;
            }
        }

        // ── 测试步骤详情编辑面板 ───────────────────────────────────────────────────
        // 步骤列表的增删/移动/复制已迁移到 VM 命令（绑定到按钮 Command）。
        // 步骤详情面板各字段已全部 TwoWay 绑定到 SelectedStep 子属性 + SelectedStepMaxRetries；
        // 面板可见性由 NullToVisibilityConverter（绑定 SelectedStep）自动控制。
        // 由于 TestStepConfig 未实现 INPC，"刷新列表"按钮触发 VM 的 RefreshStepsCommand
        // 同步 SelectedStepMaxRetries 回写 + 提示 UI 重绘（仍需 CollectionView 重置或 Items.Refresh）。

        private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
    }
}
