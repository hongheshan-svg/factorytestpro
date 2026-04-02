using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using UTF.Plugin.Abstractions;
using UTF.UI.Services;

namespace UTF.UI
{
    internal class PluginDisplayItem
    {
        public string PluginId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public int Priority { get; set; }
        public bool IsLoaded { get; set; }
        public string? ErrorCode { get; set; }
        public string? ErrorMessage { get; set; }
        public string? ManifestPath { get; set; }
        public IReadOnlyList<string> SupportedStepTypes { get; set; } = Array.Empty<string>();
        public IReadOnlyList<string> SupportedChannels { get; set; } = Array.Empty<string>();
        public string StatusIcon => IsLoaded ? "✅" : "❌";
        public string StatusColor => IsLoaded ? "#27AE60" : "#E74C3C";
    }

    public partial class PluginManagementWindow : Window
    {
        private readonly DUTMonitorManager _monitorManager;
        private List<PluginDisplayItem> _allPlugins = new();

        public PluginManagementWindow(DUTMonitorManager monitorManager)
        {
            InitializeComponent();
            _monitorManager = monitorManager;
            Loaded += Window_Loaded;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var pluginRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins");
            PluginDirectoryText.Text = $"插件目录: {pluginRoot}";
            LoadPluginData();
        }

        private void LoadPluginData()
        {
            _allPlugins.Clear();

            foreach (var meta in _monitorManager.LoadedPlugins)
            {
                _allPlugins.Add(new PluginDisplayItem
                {
                    PluginId = meta.PluginId,
                    Name = meta.Name,
                    Version = meta.Version,
                    Priority = meta.Priority,
                    IsLoaded = true,
                    SupportedStepTypes = meta.SupportedStepTypes,
                    SupportedChannels = meta.SupportedChannels
                });
            }

            if (_monitorManager.LastLoadReport != null)
            {
                foreach (var issue in _monitorManager.LastLoadReport.Issues)
                {
                    _allPlugins.Add(new PluginDisplayItem
                    {
                        PluginId = Path.GetDirectoryName(issue.ManifestPath) ?? "unknown",
                        Name = "加载失败",
                        Version = "-",
                        IsLoaded = false,
                        ErrorCode = issue.ErrorCode,
                        ErrorMessage = issue.Message,
                        ManifestPath = issue.ManifestPath
                    });
                }
            }

            UpdateSummaryBadge();
            PluginListBox.ItemsSource = _allPlugins;
        }

        private void UpdateSummaryBadge()
        {
            int loaded = _allPlugins.Count(p => p.IsLoaded);
            int failed = _allPlugins.Count(p => !p.IsLoaded);

            SummaryText.Text = failed > 0
                ? $"已加载 {loaded} 个 / 失败 {failed} 个"
                : $"已加载 {loaded} 个";

            SummaryBadge.Background = new SolidColorBrush(
                failed > 0
                    ? Color.FromRgb(0xE7, 0x4C, 0x3C)
                    : Color.FromRgb(0x27, 0xAE, 0x60));
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var keyword = SearchBox.Text.Trim().ToLowerInvariant();
            PluginListBox.ItemsSource = string.IsNullOrEmpty(keyword)
                ? _allPlugins
                : _allPlugins.Where(p =>
                    p.Name.ToLowerInvariant().Contains(keyword) ||
                    p.PluginId.ToLowerInvariant().Contains(keyword)).ToList();
        }

        private void PluginListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PluginListBox.SelectedItem is PluginDisplayItem item)
                ShowPluginDetail(item);
        }

        private void ShowPluginDetail(PluginDisplayItem item)
        {
            DetailPanel.Visibility = Visibility.Visible;

            DetailName.Text = item.Name;
            DetailVersion.Text = $"v{item.Version}";
            DetailPluginId.Text = item.PluginId;
            DetailApiVersion.Text = "1.0";
            DetailPriority.Text = item.Priority.ToString();

            StepTypesList.ItemsSource = item.SupportedStepTypes.Count > 0
                ? item.SupportedStepTypes
                : new[] { "(无)" };
            ChannelsList.ItemsSource = item.SupportedChannels.Count > 0
                ? item.SupportedChannels
                : new[] { "(无)" };

            if (item.IsLoaded)
            {
                DetailStatus.Text = "已加载";
                StatusBadge.Background = new SolidColorBrush(Color.FromRgb(0x27, 0xAE, 0x60));
                ErrorPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                DetailStatus.Text = "加载失败";
                StatusBadge.Background = new SolidColorBrush(Color.FromRgb(0xE7, 0x4C, 0x3C));
                ErrorPanel.Visibility = Visibility.Visible;
                DetailErrorCode.Text = item.ErrorCode ?? "-";
                DetailErrorMessage.Text = item.ErrorMessage ?? "-";
            }
        }

        private void RescanPlugins_Click(object sender, RoutedEventArgs e)
        {
            LoadPluginData();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
