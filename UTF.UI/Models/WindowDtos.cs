using System;
using System.Collections.Generic;
using UTF.UI.Services;

namespace UTF.UI.Models;

/// <summary>
/// 测试计划（编辑器内存模型）。
/// 从 <c>UTF.UI.TestPlanEditorWindow.xaml.cs</c> 提取至本命名空间，便于后续在视图模型 / 服务间共享。
/// </summary>
public class TestPlan
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int EstimatedDurationMinutes { get; set; }
    public bool AutoRun { get; set; }
    public bool GenerateReport { get; set; } = true;
    public List<TestPlanStep> TestSteps { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
}

/// <summary>
/// 测试计划步骤（编辑器内存模型）。
/// </summary>
public class TestPlanStep
{
    public string StepName { get; set; } = "";
    public string Description { get; set; } = "";
    public string StepType { get; set; } = "";
    public string Channel { get; set; } = "";
    public string Command { get; set; } = "";
    public string Expected { get; set; } = "";
    public int TimeoutSeconds { get; set; } = 60;
    public bool IsRequired { get; set; } = true;
}

/// <summary>
/// 用户显示信息。从 <c>UTF.UI.UserManagementWindow.xaml.cs</c> 提取至本命名空间。
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

/// <summary>
/// 仪器设备信息。从 <c>UTF.UI.DeviceManagementWindow.xaml.cs</c> 提取至本命名空间。
/// </summary>
public class InstrumentDeviceInfo
{
    public string DeviceId { get; set; } = "";
    public string DeviceName { get; set; } = "";
    public string DeviceType { get; set; } = "";
    public string Manufacturer { get; set; } = "";
    public string Model { get; set; } = "";
    public string ConnectionType { get; set; } = "";
    public string ConnectionAddress { get; set; } = "";
    public string Status { get; set; } = "";
}

/// <summary>
/// DUT 设备信息。从 <c>UTF.UI.DeviceManagementWindow.xaml.cs</c> 提取至本命名空间。
/// </summary>
public class DutDeviceInfo
{
    public string DutId { get; set; } = "";
    public string DutName { get; set; } = "";
    public string DutType { get; set; } = "";
    public string Manufacturer { get; set; } = "";
    public string Model { get; set; } = "";
    public string CommunicationType { get; set; } = "";
    public string ConnectionParams { get; set; } = "";
    public string Status { get; set; } = "";
}
