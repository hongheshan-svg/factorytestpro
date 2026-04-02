using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace UTF.UI.Services;

public class PermissionManager : IPermissionManager
{
    private const string UsersFileName = "users.json";
    private const string DefaultAdminUsername = "admin";
    private const string DefaultAdminPassword = "admin123";

    private readonly string _usersFilePath;
    private readonly Dictionary<string, UserData> _users = new();

    public UserInfo? CurrentUser { get; private set; }

    public event EventHandler<PermissionChangedEventArgs>? PermissionChanged;

    public PermissionManager(string dataDirectory = "Data")
    {
        if (!Directory.Exists(dataDirectory))
        {
            Directory.CreateDirectory(dataDirectory);
        }

        _usersFilePath = Path.Combine(dataDirectory, UsersFileName);
        InitializeSync();
    }

    private void InitializeSync()
    {
        LoadUsersSync();
        if (!_users.Any())
        {
            CreateDefaultAdminSync();
        }
    }

    private void LoadUsersSync()
    {
        try
        {
            if (File.Exists(_usersFilePath))
            {
                var json = File.ReadAllText(_usersFilePath);
                var usersData = JsonSerializer.Deserialize<Dictionary<string, UserData>>(json) ?? new();
                _users.Clear();
                foreach (var kvp in usersData)
                    _users[kvp.Key.ToLowerInvariant()] = kvp.Value;
            }
        }
        catch { }
    }

    private void CreateDefaultAdminSync()
    {
        var userData = new UserData
        {
            Username = DefaultAdminUsername,
            DisplayName = "系统管理员",
            Email = "admin@utf.com",
            PasswordHash = HashPassword(DefaultAdminPassword),
            Role = UserRole.SuperAdmin,
            CreatedAt = DateTime.Now,
            IsActive = true
        };
        _users[DefaultAdminUsername.ToLowerInvariant()] = userData;
        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
            File.WriteAllText(_usersFilePath, JsonSerializer.Serialize(_users, options));
        }
        catch { }
    }

    public async Task<LoginResult> LoginAsync(string username, string password)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                return new LoginResult
                {
                    Success = false,
                    Message = "用户名和密码不能为空"
                };
            }

            if (!_users.TryGetValue(username.ToLowerInvariant(), out var userData))
            {
                return new LoginResult
                {
                    Success = false,
                    Message = "用户不存在"
                };
            }

            if (!userData.IsActive)
            {
                return new LoginResult
                {
                    Success = false,
                    Message = "用户已被禁用"
                };
            }

            if (!VerifyPassword(password, userData.PasswordHash))
            {
                return new LoginResult
                {
                    Success = false,
                    Message = "密码错误"
                };
            }

            userData.LastLoginAt = DateTime.Now;
            await SaveUsersAsync();

            CurrentUser = ConvertToUserInfo(userData);

            return new LoginResult
            {
                Success = true,
                Message = "登录成功",
                User = CurrentUser,
                Token = GenerateToken(userData)
            };
        }
        catch (Exception ex)
        {
            return new LoginResult
            {
                Success = false,
                Message = $"登录失败: {ex.Message}"
            };
        }
    }

    public async Task LogoutAsync()
    {
        CurrentUser = null;
        await Task.CompletedTask;
    }

    public bool HasPermission(Permission permission)
    {
        return true;
    }

    public bool HasRole(UserRole role)
    {
        return true;
    }

    public async Task<IEnumerable<UserInfo>> GetAllUsersAsync()
    {
        await LoadUsersAsync();
        return _users.Values.Select(ConvertToUserInfo).ToList();
    }

    public async Task<bool> CreateUserAsync(CreateUserRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            {
                return false;
            }

            var usernameKey = request.Username.ToLowerInvariant();
            if (_users.ContainsKey(usernameKey))
            {
                return false;
            }

            var userData = new UserData
            {
                Username = request.Username,
                DisplayName = request.DisplayName,
                Email = request.Email,
                PasswordHash = HashPassword(request.Password),
                Role = request.Role,
                CustomPermissions = request.CustomPermissions,
                CreatedAt = DateTime.Now,
                IsActive = true
            };

            _users[usernameKey] = userData;
            await SaveUsersAsync();

            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> UpdateUserPermissionsAsync(string username, UserRole role, List<Permission> permissions)
    {
        try
        {
            var usernameKey = username.ToLowerInvariant();
            if (!_users.TryGetValue(usernameKey, out var userData))
            {
                return false;
            }

            var oldPermissions = GetUserPermissions(userData.Role, userData.CustomPermissions);

            userData.Role = role;
            userData.CustomPermissions = permissions ?? new List<Permission>();

            await SaveUsersAsync();

            var newPermissions = GetUserPermissions(role, permissions ?? new List<Permission>());
            var userInfo = ConvertToUserInfo(userData);

            PermissionChanged?.Invoke(this, new PermissionChangedEventArgs(userInfo, oldPermissions, newPermissions));

            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> DeleteUserAsync(string username)
    {
        try
        {
            var usernameKey = username.ToLowerInvariant();
            if (!_users.ContainsKey(usernameKey))
            {
                return false;
            }

            if (usernameKey == DefaultAdminUsername.ToLowerInvariant())
            {
                return false;
            }

            _users.Remove(usernameKey);
            await SaveUsersAsync();

            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task LoadUsersAsync()
    {
        try
        {
            if (File.Exists(_usersFilePath))
            {
                var json = await File.ReadAllTextAsync(_usersFilePath);
                var usersData = JsonSerializer.Deserialize<Dictionary<string, UserData>>(json) ?? new();

                _users.Clear();
                foreach (var kvp in usersData)
                {
                    _users[kvp.Key.ToLowerInvariant()] = kvp.Value;
                }
            }
        }
        catch
        {
        }
    }

    private async Task SaveUsersAsync()
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            var json = JsonSerializer.Serialize(_users, options);
            await File.WriteAllTextAsync(_usersFilePath, json);
        }
        catch
        {
        }
    }

    private static string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var salt = "UTF_SALT_2024";
        var saltedPassword = password + salt;
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(saltedPassword));
        return Convert.ToBase64String(hashBytes);
    }

    private static bool VerifyPassword(string password, string hash)
    {
        return HashPassword(password) == hash;
    }

    private static string GenerateToken(UserData userData)
    {
        var tokenData = $"{userData.Username}:{DateTime.Now:yyyyMMddHHmmss}";
        var tokenBytes = Encoding.UTF8.GetBytes(tokenData);
        return Convert.ToBase64String(tokenBytes);
    }

    private static Permission GetUserPermissions(UserRole role, List<Permission> customPermissions)
    {
        var rolePermissions = role switch
        {
            UserRole.SuperAdmin => Permission.AllPermissions,
            UserRole.Admin => Permission.AdminPermissions,
            UserRole.Engineer => Permission.EngineerPermissions,
            UserRole.Technician => Permission.TechnicianPermissions,
            UserRole.Operator => Permission.OperatorPermissions,
            UserRole.Observer => Permission.ObserverPermissions,
            _ => Permission.None
        };

        foreach (var permission in customPermissions)
        {
            rolePermissions |= permission;
        }

        return rolePermissions;
    }

    private static UserInfo ConvertToUserInfo(UserData userData)
    {
        return new UserInfo
        {
            Username = userData.Username,
            DisplayName = userData.DisplayName,
            Email = userData.Email,
            Role = userData.Role,
            Permissions = userData.CustomPermissions,
            CreatedAt = userData.CreatedAt,
            LastLoginAt = userData.LastLoginAt,
            IsActive = userData.IsActive
        };
    }
}

internal class UserData
{
    public string Username { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public UserRole Role { get; set; } = UserRole.Operator;
    public List<Permission> CustomPermissions { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime LastLoginAt { get; set; } = DateTime.MinValue;
    public bool IsActive { get; set; } = true;
}
