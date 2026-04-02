using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace UTF.UI.Services;

/// <summary>
/// 权限管理器接口
/// </summary>
public interface IPermissionManager
{
    /// <summary>
    /// 当前登录用户
    /// </summary>
    UserInfo? CurrentUser { get; }

    /// <summary>
    /// 用户登录
    /// </summary>
    Task<LoginResult> LoginAsync(string username, string password);

    /// <summary>
    /// 用户登出
    /// </summary>
    Task LogoutAsync();

    /// <summary>
    /// 检查用户是否有指定权限
    /// </summary>
    bool HasPermission(Permission permission);

    /// <summary>
    /// 检查用户是否有指定角色
    /// </summary>
    bool HasRole(UserRole role);

    /// <summary>
    /// 获取所有用户
    /// </summary>
    Task<IEnumerable<UserInfo>> GetAllUsersAsync();

    /// <summary>
    /// 创建用户
    /// </summary>
    Task<bool> CreateUserAsync(CreateUserRequest request);

    /// <summary>
    /// 更新用户权限
    /// </summary>
    Task<bool> UpdateUserPermissionsAsync(string username, UserRole role, List<Permission> permissions);

    /// <summary>
    /// 删除用户
    /// </summary>
    Task<bool> DeleteUserAsync(string username);

    /// <summary>
    /// 权限变更事件
    /// </summary>
    event EventHandler<PermissionChangedEventArgs>? PermissionChanged;
}

/// <summary>
/// 用户信息
/// </summary>
public record UserInfo
{
    public string Username { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string Email { get; init; } = "";
    public UserRole Role { get; init; } = UserRole.Operator;
    public List<Permission> Permissions { get; init; } = new();
    public DateTime CreatedAt { get; init; } = DateTime.Now;
    public DateTime LastLoginAt { get; init; } = DateTime.MinValue;
    public bool IsActive { get; init; } = true;
}

/// <summary>
/// 用户角色
/// </summary>
public enum UserRole
{
    SuperAdmin = 0,
    Admin = 1,
    Engineer = 2,
    Technician = 3,
    Operator = 4,
    Observer = 5
}

/// <summary>
/// 权限枚举
/// </summary>
[Flags]
public enum Permission : long
{
    None = 0,
    SystemAdmin = 1L << 0,
    UserManagement = 1L << 1,
    SystemConfig = 1L << 2,
    DeviceManagement = 1L << 10,
    DeviceConfig = 1L << 11,
    DeviceCalibration = 1L << 12,
    DeviceDiscovery = 1L << 13,
    TestPlanManagement = 1L << 20,
    TestPlanCreate = 1L << 21,
    TestPlanEdit = 1L << 22,
    TestPlanDelete = 1L << 23,
    TestExecution = 1L << 30,
    TestStart = 1L << 31,
    TestStop = 1L << 32,
    TestPause = 1L << 33,
    DutManagement = 1L << 40,
    DutConfig = 1L << 41,
    DutConnection = 1L << 42,
    MultiDutTest = 1L << 43,
    DataView = 1L << 50,
    DataExport = 1L << 51,
    DataDelete = 1L << 52,
    ReportGeneration = 1L << 53,
    LogView = 1L << 60,
    LogExport = 1L << 61,
    LogClear = 1L << 62,
    AllPermissions = ~0L,
    AdminPermissions = SystemAdmin | UserManagement | SystemConfig |
                      DeviceManagement | DeviceConfig | DeviceCalibration |
                      TestPlanManagement | TestPlanCreate | TestPlanEdit | TestPlanDelete |
                      TestExecution | TestStart | TestStop | TestPause |
                      DutManagement | DutConfig | DutConnection | MultiDutTest |
                      DataView | DataExport | DataDelete | ReportGeneration |
                      LogView | LogExport | LogClear,
    EngineerPermissions = DeviceConfig | DeviceCalibration | DeviceDiscovery |
                         TestPlanManagement | TestPlanCreate | TestPlanEdit |
                         TestExecution | TestStart | TestStop | TestPause |
                         DutManagement | DutConfig | DutConnection | MultiDutTest |
                         DataView | DataExport | ReportGeneration |
                         LogView,
    TechnicianPermissions = TestExecution | TestStart | TestStop | TestPause |
                           DutConnection | MultiDutTest |
                           DataView | ReportGeneration |
                           LogView,
    OperatorPermissions = TestExecution | TestStart | TestStop |
                         DataView | LogView,
    ObserverPermissions = DataView | LogView
}

/// <summary>
/// 登录结果
/// </summary>
public record LoginResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = "";
    public UserInfo? User { get; init; }
    public string? Token { get; init; }
}

/// <summary>
/// 创建用户请求
/// </summary>
public record CreateUserRequest
{
    public string Username { get; init; } = "";
    public string Password { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string Email { get; init; } = "";
    public UserRole Role { get; init; } = UserRole.Operator;
    public List<Permission> CustomPermissions { get; init; } = new();
}

/// <summary>
/// 权限变更事件参数
/// </summary>
public class PermissionChangedEventArgs : EventArgs
{
    public UserInfo User { get; }
    public Permission OldPermissions { get; }
    public Permission NewPermissions { get; }

    public PermissionChangedEventArgs(UserInfo user, Permission oldPermissions, Permission newPermissions)
    {
        User = user;
        OldPermissions = oldPermissions;
        NewPermissions = newPermissions;
    }
}
