using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UTF.UI.Models;
using UTF.UI.Services;

namespace UTF.UI.ViewModels;

/// <summary>
/// View model for the process/product template pack picker dialog.
/// </summary>
public partial class TemplatePackPickerViewModel : ObservableObject
{
    private readonly ITemplatePackService _templatePackService;
    private readonly IDialogService _dialogService;
    private readonly UTF.Logging.ILogger? _logger;

    public TemplatePackPickerViewModel(
        ITemplatePackService templatePackService,
        IDialogService dialogService,
        UTF.Logging.ILogger? logger = null)
    {
        _templatePackService = templatePackService
            ?? throw new ArgumentNullException(nameof(templatePackService));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _logger = logger;
    }

    /// <summary>Available packs from the templates directory.</summary>
    public ObservableCollection<TemplatePackInfo> Packs { get; } = new();

    /// <summary>Currently selected pack in the list.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplySelectedPackCommand))]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    [NotifyPropertyChangedFor(nameof(PreviewProductName))]
    [NotifyPropertyChangedFor(nameof(PreviewProductModel))]
    [NotifyPropertyChangedFor(nameof(PreviewStepCountText))]
    [NotifyPropertyChangedFor(nameof(PreviewDescription))]
    [NotifyPropertyChangedFor(nameof(PreviewIndustry))]
    [NotifyPropertyChangedFor(nameof(PreviewFileName))]
    private TemplatePackInfo? _selectedPack;

    /// <summary>Status / empty-state message shown above the list.</summary>
    [ObservableProperty]
    private string _statusMessage = string.Empty;

    /// <summary>True while Apply is in progress.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplySelectedPackCommand))]
    private bool _isBusy;

    /// <summary>True when a pack is selected.</summary>
    public bool HasSelection => SelectedPack is not null;

    public string PreviewProductName =>
        string.IsNullOrWhiteSpace(SelectedPack?.ProductName) ? "—" : SelectedPack!.ProductName!;

    public string PreviewProductModel =>
        string.IsNullOrWhiteSpace(SelectedPack?.ProductModel) ? "—" : SelectedPack!.ProductModel!;

    public string PreviewStepCountText =>
        SelectedPack is null ? "—" : $"{SelectedPack.StepCount} 个步骤";

    public string PreviewDescription =>
        string.IsNullOrWhiteSpace(SelectedPack?.Description) ? "（无描述）" : SelectedPack!.Description;

    public string PreviewIndustry =>
        string.IsNullOrWhiteSpace(SelectedPack?.Industry) ? "通用" : SelectedPack!.Industry!;

    public string PreviewFileName =>
        SelectedPack?.FileName ?? "—";

    /// <summary>
    /// Raised when a pack was successfully applied (window should close with DialogResult=true).
    /// </summary>
    public event EventHandler? PackApplied;

    /// <summary>Load catalog from disk into <see cref="Packs"/>.</summary>
    public void LoadPacks()
    {
        Packs.Clear();
        SelectedPack = null;

        var packs = _templatePackService.GetAvailablePacks();
        foreach (var pack in packs)
        {
            Packs.Add(pack);
        }

        if (Packs.Count == 0)
        {
            StatusMessage = string.IsNullOrWhiteSpace(_templatePackService.TemplatesDirectory)
                ? "未找到模板目录。"
                : $"未找到模板（目录: {_templatePackService.TemplatesDirectory}）";
        }
        else
        {
            StatusMessage = $"共 {Packs.Count} 个工艺包 · {_templatePackService.TemplatesDirectory}";
            SelectedPack = Packs[0];
        }
    }

    private bool CanApplySelectedPack() => SelectedPack is not null && !IsBusy;

    /// <summary>
    /// Confirm and apply the selected pack as the active unified-config.json.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanApplySelectedPack))]
    private async Task ApplySelectedPackAsync()
    {
        if (SelectedPack is null || IsBusy)
        {
            return;
        }

        var confirmed = _dialogService.ShowConfirmation(
            $"将替换当前 unified-config.json 为工艺包「{SelectedPack.DisplayName}」。\n\n" +
            "当前配置会先备份到 config 目录。是否继续？",
            "应用工艺包/模板");

        if (!confirmed)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var backupPath = await _templatePackService
                .ApplyPackAsync(SelectedPack.FullPath, backupCurrent: true)
                .ConfigureAwait(true);

            var backupNote = string.IsNullOrEmpty(backupPath)
                ? "（无现有配置可备份或备份失败）"
                : $"\n备份文件: {backupPath}";

            _dialogService.ShowInformation(
                $"已应用工艺包「{SelectedPack.DisplayName}」。{backupNote}\n\n" +
                "主界面将刷新 DUT 列表与产品型号。",
                "应用成功");

            PackApplied?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _logger?.Error("应用工艺包失败", ex);
            _dialogService.ShowError($"应用工艺包失败: {ex.Message}", "错误");
        }
        finally
        {
            IsBusy = false;
        }
    }
}
