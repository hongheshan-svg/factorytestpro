using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using UTF.UI.Localization;
using UTF.UI.Services;

namespace UTF.UI;

/// <summary>
/// 登录窗口
/// </summary>
public partial class LoginWindow : Window
{
    private readonly IPermissionManager _permissionManager;
    private readonly ILanguageManager _languageManager;
    private const string RememberMeConfigFile = "rememberme.json";
    
    public bool LoginSuccessful { get; private set; }
    public UserInfo? LoggedInUser { get; private set; }
    
    public LoginWindow(IPermissionManager permissionManager)
    {
        try
        {
            System.Console.WriteLine("LoginWindow constructor started");
            
            System.Console.WriteLine("Calling InitializeComponent");
            InitializeComponent();
            System.Console.WriteLine("InitializeComponent completed");
            
            _permissionManager = permissionManager;
            
            // 使用 LocalizationService 中已存在的语言管理器，而不是创建新的
            System.Console.WriteLine("Getting LanguageManager from LocalizationService");
            _languageManager = LocalizationService.GetLanguageManager();
            System.Console.WriteLine($"LanguageManager obtained, current language: {_languageManager.CurrentLanguage}");
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"Exception in LoginWindow constructor: {ex}");
            var errorTitle = LocalizationHelper.GetString("Common.Error", "错误");
            var initError = LocalizationHelper.GetString("Login.InitError", "登录窗口初始化错误");
            MessageBox.Show($"{initError}: {ex.Message}\n\n详细信息:\n{ex}", errorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            throw;
        }
        
        // 加载记住的用户名
        LoadRememberedUsername();
        
        // 隐藏语言选择框（固定使用中文）
        if (LanguageComboBox != null)
        {
            LanguageComboBox.Visibility = Visibility.Collapsed;
        }
        
        // 焦点设置
        UsernameTextBox.Focus();
    }
    
    private void OnLanguageChanged(object? sender, LanguageChangedEventArgs e)
    {
        // 语言更改时刷新界面文本
        Dispatcher.Invoke(() =>
        {
            System.Console.WriteLine($"OnLanguageChanged: {e.OldLanguage} -> {e.NewLanguage}");
            
            // 更新窗口标题
            Title = _languageManager.GetString("Login.Title", "用户登录 - Universal Test Framework");
            
            // 更新登录提示文本
            LoginHintTitle.Text = _languageManager.GetString("Login.DefaultAccount", "💡 登录提示");
            LoginHintText.Text = _languageManager.GetString("Login.LoginHint", "请输入您的用户名和密码进行登录");
            LoginHintDefaultAdmin.Text = _languageManager.GetString("Login.DefaultAdmin", "系统默认管理员");
            
            // 更新下拉框选择（不触发 SelectionChanged 事件）
            var currentItem = (LanguageComboBox.ItemsSource as System.Collections.Generic.List<LanguageDisplayItem>)
                ?.FirstOrDefault(l => l.Code == e.NewLanguage);
            if (currentItem != null && LanguageComboBox.SelectedItem != currentItem)
            {
                LanguageComboBox.SelectionChanged -= LanguageComboBox_SelectionChanged;
                LanguageComboBox.SelectedItem = currentItem;
                LanguageComboBox.SelectionChanged += LanguageComboBox_SelectionChanged;
                System.Console.WriteLine($"Language dropdown updated to: {currentItem.DisplayName}");
            }
        });
    }
    
    protected override void OnClosed(EventArgs e)
    {
        // 取消订阅语言更改事件
        if (_languageManager != null)
        {
            _languageManager.LanguageChanged -= OnLanguageChanged;
        }
        base.OnClosed(e);
    }
    
    private async void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        await PerformLoginAsync();
    }
    
    private async System.Threading.Tasks.Task PerformLoginAsync()
    {
        try
        {
            // 禁用登录按钮，防止重复点击
            LoginButton.IsEnabled = false;
            StatusTextBlock.Visibility = Visibility.Collapsed;
            
            var username = UsernameTextBox.Text.Trim();
            var password = PasswordBox.Password;
            
            if (string.IsNullOrWhiteSpace(username))
            {
                ShowError(_languageManager.GetString("Login.EmptyUsername", "请输入用户名"));
                return;
            }
            
            if (string.IsNullOrWhiteSpace(password))
            {
                ShowError(_languageManager.GetString("Login.EmptyPassword", "请输入密码"));
                return;
            }
            
            // 显示登录中状态
            StatusTextBlock.Text = _languageManager.GetString("Login.LoggingIn", "正在登录...");
            StatusTextBlock.Foreground = System.Windows.Media.Brushes.Blue;
            StatusTextBlock.Visibility = Visibility.Visible;
            
            // 执行登录
            var result = await _permissionManager.LoginAsync(username, password);
            
            if (result.Success)
            {
                LoginSuccessful = true;
                LoggedInUser = result.User;
                
                // 保存或清除记住的用户名
                if (RememberMeCheckBox.IsChecked == true)
                {
                    SaveRememberedUsername(username);
                }
                else
                {
                    ClearRememberedUsername();
                }
                
                StatusTextBlock.Text = _languageManager.GetString("Login.LoginSuccess", "登录成功！");
                StatusTextBlock.Foreground = System.Windows.Media.Brushes.Green;
                
                // 延迟关闭窗口，让用户看到成功消息
                await System.Threading.Tasks.Task.Delay(500);
                DialogResult = true;
                Close();
            }
            else
            {
                ShowError(result.Message);
            }
        }
        catch (Exception ex)
        {
            var loginFailedMsg = _languageManager.GetString("Login.LoginFailed", "登录失败");
            ShowError($"{loginFailedMsg}: {ex.Message}");
        }
        finally
        {
            LoginButton.IsEnabled = true;
        }
    }
    
    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        LoginSuccessful = false;
        DialogResult = false;
        Close();
    }
    
    private void ShowError(string message)
    {
        StatusTextBlock.Text = message;
        StatusTextBlock.Foreground = System.Windows.Media.Brushes.Red;
        StatusTextBlock.Visibility = Visibility.Visible;
    }
    
    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter)
        {
            _ = PerformLoginAsync();
        }
    }
    
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        
        // 添加键盘事件处理
        KeyDown += Window_KeyDown;
    }
    
    private void InitializeLanguageSelection()
    {
        System.Console.WriteLine($"InitializeLanguageSelection: Current language = {_languageManager.CurrentLanguage}");
        
        // 创建语言显示项
        var languageItems = _languageManager.SupportedLanguages.Select(lang => new LanguageDisplayItem
        {
            Code = lang.Code,
            DisplayName = $"{lang.Flag} {lang.NativeName}",
            LanguageInfo = lang
        }).ToList();
        
        System.Console.WriteLine($"InitializeLanguageSelection: Found {languageItems.Count} languages");
        foreach (var item in languageItems)
        {
            System.Console.WriteLine($"  - {item.Code}: {item.DisplayName}");
        }
        
        LanguageComboBox.ItemsSource = languageItems;
        
        // 选择当前语言
        var currentItem = languageItems.FirstOrDefault(l => l.Code == _languageManager.CurrentLanguage);
        if (currentItem != null)
        {
            LanguageComboBox.SelectedItem = currentItem;
            System.Console.WriteLine($"InitializeLanguageSelection: Selected {currentItem.DisplayName}");
        }
        else
        {
            System.Console.WriteLine($"InitializeLanguageSelection: WARNING - Could not find language item for {_languageManager.CurrentLanguage}");
        }
    }
    
    private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LanguageComboBox.SelectedItem is LanguageDisplayItem selectedItem)
        {
            System.Console.WriteLine($"LanguageComboBox_SelectionChanged: User selected {selectedItem.Code} ({selectedItem.DisplayName})");
            _languageManager.SetLanguage(selectedItem.Code);
        }
    }
    
    public ILanguageManager GetLanguageManager()
    {
        return _languageManager;
    }
    
    /// <summary>
    /// 加载记住的用户名
    /// </summary>
    private void LoadRememberedUsername()
    {
        try
        {
            if (File.Exists(RememberMeConfigFile))
            {
                var json = File.ReadAllText(RememberMeConfigFile);
                var config = JsonSerializer.Deserialize<RememberMeConfig>(json);
                
                if (config != null && !string.IsNullOrEmpty(config.Username))
                {
                    UsernameTextBox.Text = config.Username;
                    RememberMeCheckBox.IsChecked = true;
                    
                    // 将焦点移到密码框
                    PasswordBox.Focus();
                }
            }
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"加载记住的用户名失败: {ex.Message}");
            // 不显示错误提示，静默失败
        }
    }
    
    /// <summary>
    /// 保存记住的用户名
    /// </summary>
    private void SaveRememberedUsername(string username)
    {
        try
        {
            var config = new RememberMeConfig
            {
                Username = username,
                SavedAt = DateTime.Now
            };
            
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            
            File.WriteAllText(RememberMeConfigFile, json);
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"保存用户名失败: {ex.Message}");
            // 不显示错误提示，静默失败
        }
    }
    
    /// <summary>
    /// 清除记住的用户名
    /// </summary>
    private void ClearRememberedUsername()
    {
        try
        {
            if (File.Exists(RememberMeConfigFile))
            {
                File.Delete(RememberMeConfigFile);
            }
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"清除记住的用户名失败: {ex.Message}");
            // 不显示错误提示，静默失败
        }
    }
}

/// <summary>
/// 语言显示项
/// </summary>
public class LanguageDisplayItem
{
    public string Code { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public LanguageInfo LanguageInfo { get; set; } = new();
}

/// <summary>
/// 记住我配置
/// </summary>
public class RememberMeConfig
{
    public string Username { get; set; } = "";
    public DateTime SavedAt { get; set; }
}
