using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using UTF.UI.Services;

namespace UTF.UI;

/// <summary>
/// 权限管理对话框
/// </summary>
public partial class PermissionManagementDialog : Window
{
    private readonly UserDisplayInfo _user;
    private readonly IPermissionManager _permissionManager;
    private UserRole _originalRole;
    private List<Permission> _originalPermissions;
    private readonly List<CheckBox> _allPermissionCheckBoxes = new();
    
    public PermissionManagementDialog(UserDisplayInfo user, IPermissionManager permissionManager)
    {
        InitializeComponent();
        _user = user;
        _permissionManager = permissionManager;
        _originalRole = user.Role;
        _originalPermissions = new List<Permission>(user.Permissions);
        
        InitializeDialog();
        LoadUserPermissions();
    }
    
    private void InitializeDialog()
    {
        TitleText.Text = $"权限管理 - {_user.DisplayName}";
        UserNameText.Text = $"{_user.DisplayName} ({_user.Username})";
        
        // 设置角色下拉框
        foreach (ComboBoxItem item in RoleComboBox.Items)
        {
            if (item.Tag.ToString() == _user.Role.ToString())
            {
                RoleComboBox.SelectedItem = item;
                break;
            }
        }
        
        // 收集所有权限复选框
        CollectPermissionCheckBoxes();
    }
    
    private void CollectPermissionCheckBoxes()
    {
        _allPermissionCheckBoxes.Clear();
        
        // 收集所有面板中的权限复选框
        var panels = new[]
        {
            SystemPermissionsPanel,
            DevicePermissionsPanel, 
            TestPermissionsPanel,
            DutPermissionsPanel,
            DataPermissionsPanel,
            LogPermissionsPanel
        };
        
        foreach (var panel in panels)
        {
            foreach (var child in panel.Children.OfType<CheckBox>())
            {
                _allPermissionCheckBoxes.Add(child);
            }
        }
    }
    
    private void LoadUserPermissions()
    {
        // 根据角色加载默认权限
        var rolePermissions = GetRolePermissions(_user.Role);
        
        // 设置权限复选框状态
        foreach (var checkBox in _allPermissionCheckBoxes)
        {
            if (Enum.TryParse<Permission>(checkBox.Tag.ToString(), out var permission))
            {
                // 检查是否有此权限（角色默认权限或自定义权限）
                var hasPermission = rolePermissions.HasFlag(permission) || _user.Permissions.Contains(permission);
                checkBox.IsChecked = hasPermission;
                
                // 如果是角色默认权限，设置为只读（灰显）
                if (rolePermissions.HasFlag(permission))
                {
                    checkBox.IsEnabled = false;
                    checkBox.Foreground = System.Windows.Media.Brushes.Gray;
                }
                else
                {
                    checkBox.IsEnabled = true;
                    checkBox.Foreground = System.Windows.Media.Brushes.Black;
                }
            }
        }
    }
    
    private static Permission GetRolePermissions(UserRole role)
    {
        return role switch
        {
            UserRole.SuperAdmin => Permission.AllPermissions,
            UserRole.Admin => Permission.AdminPermissions,
            UserRole.Engineer => Permission.EngineerPermissions,
            UserRole.Technician => Permission.TechnicianPermissions,
            UserRole.Operator => Permission.OperatorPermissions,
            UserRole.Observer => Permission.ObserverPermissions,
            _ => Permission.None
        };
    }
    
    private void RoleComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (RoleComboBox.SelectedItem is ComboBoxItem item)
        {
            if (Enum.TryParse<UserRole>(item.Tag.ToString(), out var newRole))
            {
                _user.Role = newRole;
                LoadUserPermissions(); // 重新加载权限显示
            }
        }
    }
    
    private void SaveBtn_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // 收集自定义权限（非角色默认权限）
            var customPermissions = new List<Permission>();
            var rolePermissions = GetRolePermissions(_user.Role);
            
            foreach (var checkBox in _allPermissionCheckBoxes)
            {
                if (checkBox.IsChecked == true && checkBox.IsEnabled && 
                    Enum.TryParse<Permission>(checkBox.Tag.ToString(), out var permission))
                {
                    // 只保存非角色默认的自定义权限
                    if (!rolePermissions.HasFlag(permission))
                    {
                        customPermissions.Add(permission);
                    }
                }
            }
            
            // 更新用户权限
            var result = _permissionManager.UpdateUserPermissionsAsync(_user.Username, _user.Role, customPermissions).Result;
            
            if (result)
            {
                MessageBox.Show("权限更新成功！", "操作成功", MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show("权限更新失败！", "操作失败", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"权限更新失败: {ex.Message}", "操作失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    
    private void ResetBtn_Click(object sender, RoutedEventArgs e)
    {
        // 恢复原始状态
        _user.Role = _originalRole;
        _user.Permissions = new List<Permission>(_originalPermissions);
        
        // 重新设置角色下拉框
        foreach (ComboBoxItem item in RoleComboBox.Items)
        {
            if (item.Tag.ToString() == _originalRole.ToString())
            {
                RoleComboBox.SelectedItem = item;
                break;
            }
        }
        
        // 重新加载权限
        LoadUserPermissions();
    }
    
    private void CancelBtn_Click(object sender, RoutedEventArgs e)
    {
        // 恢复原始状态
        _user.Role = _originalRole;
        _user.Permissions = new List<Permission>(_originalPermissions);
        
        DialogResult = false;
        Close();
    }
}
