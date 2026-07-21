using System.Windows;
using UTF.UI.Services;

namespace UTF.UI;

public partial class LoginWindow : Window
{
    private readonly IPermissionManager _permissionManager;
    private bool _isBootstrap;

    public LoginWindow(IPermissionManager permissionManager)
    {
        _permissionManager = permissionManager;
        InitializeComponent();
        Loaded += LoginWindow_Loaded;
    }

    private async void LoginWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _isBootstrap = !await _permissionManager.HasUsersAsync();
        if (_isBootstrap)
        {
            Title = "First-time setup - Universal Test Framework";
            TitleText.Text = "Create the first administrator";
            SubtitleText.Text = "Account data is stored for the current Windows user only";
            ConfirmPanel.Visibility = Visibility.Visible;
            SubmitButton.Content = "Create and sign in";
        }

        UsernameTextBox.Focus();
    }

    private async void Submit_Click(object sender, RoutedEventArgs e)
    {
        SubmitButton.IsEnabled = false;
        MessageText.Text = string.Empty;
        try
        {
            var username = UsernameTextBox.Text.Trim();
            var password = PasswordBox.Password;
            if (_isBootstrap)
            {
                if (!string.Equals(password, ConfirmPasswordBox.Password, StringComparison.Ordinal))
                {
                    MessageText.Text = "The passwords do not match.";
                    return;
                }

                if (!await _permissionManager.BootstrapAdminAsync(username, password, username))
                {
                    MessageText.Text = "Cannot create the administrator. Use a 3-64 character username and a password of at least 12 characters.";
                    return;
                }
            }

            var result = await _permissionManager.LoginAsync(username, password);
            if (!result.Success)
            {
                MessageText.Text = result.Message;
                return;
            }

            DialogResult = true;
        }
        finally
        {
            SubmitButton.IsEnabled = true;
        }
    }
}
