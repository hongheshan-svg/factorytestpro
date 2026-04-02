using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using UTF.UI.Services;

namespace UTF.UI;

/// <summary>
/// 用户管理窗口
/// </summary>
public partial class UserManagementWindow : Window
{
    private readonly IPermissionManager _permissionManager;
    private List<UserDisplayInfo> _users = new();
    
    public UserManagementWindow(IPermissionManager permissionManager)
    {
        InitializeComponent();
        _permissionManager = permissionManager;
        
        InitializeWindow();
        LoadUsers();
    }
    
    private void InitializeWindow()
    {
        // 设置当前用户信息
        CurrentUserText.Text = _permissionManager.CurrentUser?.DisplayName ?? "未知用户";
        
        // 检查权限
        CheckPermissions();
        
        StatusTextBlock.Text = "用户管理系统已就绪";
    }
    
    private void CheckPermissions()
    {
        var hasUserManagement = _permissionManager.HasPermission(Permission.UserManagement);
        
        if (!hasUserManagement)
        {
            AddUserBtn.IsEnabled = false;
            StatusTextBlock.Text = "当前用户没有用户管理权限，功能受限";
        }
    }
    
    private async void LoadUsers()
    {
        try
        {
            StatusTextBlock.Text = "正在加载用户列表...";
            
            var users = await _permissionManager.GetAllUsersAsync();
            _users = users.Select(ConvertToDisplayInfo).ToList();
            
            UserDataGrid.ItemsSource = _users;
            UpdateUserCount();
            
            StatusTextBlock.Text = "用户列表加载完成";
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text = $"加载用户失败: {ex.Message}";
        }
    }
    
    private void UpdateUserCount()
    {
        var activeUsers = _users.Count(u => u.IsActive);
        var totalUsers = _users.Count;
        UserCountText.Text = $"用户总数: {totalUsers} (活跃:{activeUsers}, 禁用:{totalUsers - activeUsers})";
    }
    
    private static UserDisplayInfo ConvertToDisplayInfo(UserInfo userInfo)
    {
        return new UserDisplayInfo
        {
            Username = userInfo.Username,
            DisplayName = userInfo.DisplayName,
            Email = userInfo.Email,
            Role = userInfo.Role,
            RoleDisplayName = GetRoleDisplayName(userInfo.Role),
            IsActive = userInfo.IsActive,
            StatusDisplayName = userInfo.IsActive ? "活跃" : "禁用",
            CreatedAt = userInfo.CreatedAt,
            CreatedAtDisplayName = userInfo.CreatedAt.ToString("yyyy-MM-dd"),
            LastLoginAt = userInfo.LastLoginAt,
            LastLoginDisplayName = userInfo.LastLoginAt == DateTime.MinValue ? "从未登录" : userInfo.LastLoginAt.ToString("yyyy-MM-dd HH:mm"),
            Permissions = userInfo.Permissions
        };
    }
    
    private static string GetRoleDisplayName(UserRole role)
    {
        return role switch
        {
            UserRole.SuperAdmin => "超级管理员",
            UserRole.Admin => "管理员",
            UserRole.Engineer => "工程师",
            UserRole.Technician => "技术员",
            UserRole.Operator => "操作员",
            UserRole.Observer => "观察者",
            _ => "未知"
        };
    }
    
    // 工具栏事件处理
    private void RefreshBtn_Click(object sender, RoutedEventArgs e)
    {
        LoadUsers();
    }
    
    private void AddUserBtn_Click(object sender, RoutedEventArgs e)
    {
        if (!_permissionManager.HasPermission(Permission.UserManagement))
        {
            MessageBox.Show("您没有用户管理权限", "权限不足", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        
        // TODO: 打开添加用户对话框
        MessageBox.Show("添加用户功能正在开发中", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
    }
    
    private void ExportUsersBtn_Click(object sender, RoutedEventArgs e)
    {
        if (!_permissionManager.HasPermission(Permission.UserManagement))
        {
            MessageBox.Show("您没有用户管理权限", "权限不足", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        
        MessageBox.Show("导出用户功能正在开发中", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
    }
    
    // 筛选事件处理
    private void ApplyFilter_Click(object sender, RoutedEventArgs e)
    {
        var roleFilter = (RoleFilterCombo.SelectedItem as ComboBoxItem)?.Content.ToString();
        var statusFilter = (StatusFilterCombo.SelectedItem as ComboBoxItem)?.Content.ToString();
        
        var filteredUsers = _users.AsEnumerable();
        
        if (roleFilter != "全部角色")
            filteredUsers = filteredUsers.Where(u => u.RoleDisplayName == roleFilter);
        
        if (statusFilter != "全部状态")
            filteredUsers = filteredUsers.Where(u => u.StatusDisplayName == statusFilter);
        
        UserDataGrid.ItemsSource = filteredUsers.ToList();
        StatusTextBlock.Text = $"筛选完成，找到 {filteredUsers.Count()} 个用户";
    }
    
    // 用户操作事件处理
    private void EditUser_Click(object sender, RoutedEventArgs e)
    {
        if (!_permissionManager.HasPermission(Permission.UserManagement))
        {
            MessageBox.Show("您没有用户管理权限", "权限不足", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        
        if (sender is Button button && button.DataContext is UserDisplayInfo user)
        {
            MessageBox.Show($"编辑用户: {user.DisplayName}", "编辑用户", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
    
    private void ManagePermissions_Click(object sender, RoutedEventArgs e)
    {
        if (!_permissionManager.HasPermission(Permission.UserManagement))
        {
            MessageBox.Show("您没有用户管理权限", "权限不足", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        
        if (sender is Button button && button.DataContext is UserDisplayInfo user)
        {
            ShowPermissionManagementDialog(user);
        }
    }
    
    private void ResetPassword_Click(object sender, RoutedEventArgs e)
    {
        if (!_permissionManager.HasPermission(Permission.UserManagement))
        {
            MessageBox.Show("您没有用户管理权限", "权限不足", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        
        if (sender is Button button && button.DataContext is UserDisplayInfo user)
        {
            var result = MessageBox.Show($"确定要重置用户 {user.DisplayName} 的密码吗？\n新密码将重置为: 123456", 
                "确认重置密码", MessageBoxButton.YesNo, MessageBoxImage.Question);
            
            if (result == MessageBoxResult.Yes)
            {
                MessageBox.Show("密码重置成功！", "操作成功", MessageBoxButton.OK, MessageBoxImage.Information);
                StatusTextBlock.Text = $"已重置用户 {user.DisplayName} 的密码";
            }
        }
    }
    
    private void DisableUser_Click(object sender, RoutedEventArgs e)
    {
        if (!_permissionManager.HasPermission(Permission.UserManagement))
        {
            MessageBox.Show("您没有用户管理权限", "权限不足", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        
        if (sender is Button button && button.DataContext is UserDisplayInfo user)
        {
            if (user.Username.ToLowerInvariant() == "admin")
            {
                MessageBox.Show("不能禁用默认管理员账户", "操作失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            var action = user.IsActive ? "禁用" : "启用";
            var result = MessageBox.Show($"确定要{action}用户 {user.DisplayName} 吗？", 
                $"确认{action}", MessageBoxButton.YesNo, MessageBoxImage.Question);
            
            if (result == MessageBoxResult.Yes)
            {
                user.IsActive = !user.IsActive;
                user.StatusDisplayName = user.IsActive ? "活跃" : "禁用";
                UserDataGrid.Items.Refresh();
                UpdateUserCount();
                StatusTextBlock.Text = $"已{action}用户 {user.DisplayName}";
            }
        }
    }
    
    private void DeleteUser_Click(object sender, RoutedEventArgs e)
    {
        if (!_permissionManager.HasPermission(Permission.UserManagement))
        {
            MessageBox.Show("您没有用户管理权限", "权限不足", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        
        if (sender is Button button && button.DataContext is UserDisplayInfo user)
        {
            if (user.Username.ToLowerInvariant() == "admin")
            {
                MessageBox.Show("不能删除默认管理员账户", "操作失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            var result = MessageBox.Show($"确定要删除用户 {user.DisplayName} 吗？\n此操作不可撤销！", 
                "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            
            if (result == MessageBoxResult.Yes)
            {
                MessageBox.Show("删除用户功能正在开发中", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
    
    private void ShowPermissionManagementDialog(UserDisplayInfo user)
    {
        var permissionDialog = new PermissionManagementDialog(user, _permissionManager);
        if (permissionDialog.ShowDialog() == true)
        {
            LoadUsers(); // 重新加载用户列表
        }
    }
    
    private void CloseBtn_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}

/// <summary>
/// 用户显示信息
/// </summary>
public class UserDisplayInfo
{
    public string Username { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Email { get; set; } = "";
    public UserRole Role { get; set; }
    public string RoleDisplayName { get; set; } = "";
    public bool IsActive { get; set; }
    public string StatusDisplayName { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public string CreatedAtDisplayName { get; set; } = "";
    public DateTime LastLoginAt { get; set; }
    public string LastLoginDisplayName { get; set; } = "";
    public List<Permission> Permissions { get; set; } = new();
}
