using System.Collections.Concurrent;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace UTF.UI.Services;

public sealed class PermissionManager : IPermissionManager, IDisposable
{
    private const string UsersFileName = "users.json";
    private const int PasswordIterations = 210_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(5);

    private readonly string _usersFilePath;
    private readonly ConcurrentDictionary<string, UserData> _users =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, LoginAttemptState> _loginAttempts =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _fileLock = new(1, 1);
    private UserInfo? _currentUser;
    private bool _disposed;

    public PermissionManager(string? dataDirectory = null)
    {
        var root = dataDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "UniversalTestFramework");
        Directory.CreateDirectory(root);
        _usersFilePath = Path.Combine(root, UsersFileName);
        LoadUsersSync();
    }

    public UserInfo? CurrentUser => _currentUser;

    public event EventHandler<PermissionChangedEventArgs>? PermissionChanged;

    public Task<bool> HasUsersAsync() => Task.FromResult(!_users.IsEmpty);

    public async Task<bool> BootstrapAdminAsync(string username, string password, string displayName)
    {
        ThrowIfDisposed();
        if (!_users.IsEmpty || !IsValidUsername(username) || !IsStrongPassword(password))
        {
            return false;
        }

        var key = NormalizeUsername(username);
        var user = new UserData
        {
            Username = username.Trim(),
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? username.Trim() : displayName.Trim(),
            PasswordHash = HashPassword(password),
            Role = UserRole.SuperAdmin,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        if (!_users.TryAdd(key, user))
        {
            return false;
        }

        try
        {
            await SaveUsersAsync().ConfigureAwait(false);
            return true;
        }
        catch
        {
            _users.TryRemove(key, out _);
            return false;
        }
    }

    public void SignInAsDevelopmentUser()
    {
        ThrowIfDisposed();

        var existingAdmin = _users.Values
            .Where(user => user.IsActive && user.Role is UserRole.SuperAdmin or UserRole.Admin)
            .OrderBy(user => user.Role)
            .ThenBy(user => user.Username, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        if (existingAdmin != null)
        {
            existingAdmin.LastLoginAt = DateTime.UtcNow;
            _currentUser = ConvertToUserInfo(existingAdmin);
            return;
        }

        _currentUser = new UserInfo
        {
            Username = "dev",
            DisplayName = "Development (skip login)",
            Email = "dev@localhost",
            Role = UserRole.SuperAdmin,
            Permissions = new List<Permission>(),
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
            IsActive = true
        };
    }

    public async Task<LoginResult> LoginAsync(string username, string password)
    {
        ThrowIfDisposed();
        var key = NormalizeUsername(username);
        if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(password))
        {
            return FailedLogin("Invalid username or password.");
        }

        var attempt = _loginAttempts.GetOrAdd(key, _ => new LoginAttemptState());
        lock (attempt)
        {
            if (attempt.LockedUntilUtc > DateTime.UtcNow)
            {
                return FailedLogin("Too many sign-in attempts. Try again later.");
            }
        }

        if (!_users.TryGetValue(key, out var user) || !user.IsActive ||
            !VerifyPassword(password, user.PasswordHash, out var needsRehash))
        {
            RegisterFailedAttempt(attempt);
            return FailedLogin("Invalid username or password.");
        }

        lock (attempt)
        {
            attempt.FailedCount = 0;
            attempt.LockedUntilUtc = DateTime.MinValue;
        }

        var previousLogin = user.LastLoginAt;
        var previousHash = user.PasswordHash;
        user.LastLoginAt = DateTime.UtcNow;
        if (needsRehash)
        {
            user.PasswordHash = HashPassword(password);
        }

        try
        {
            await SaveUsersAsync().ConfigureAwait(false);
        }
        catch
        {
            user.LastLoginAt = previousLogin;
            user.PasswordHash = previousHash;
            return FailedLogin("The sign-in state could not be saved.");
        }

        _currentUser = ConvertToUserInfo(user);
        return new LoginResult
        {
            Success = true,
            Message = "Sign-in succeeded.",
            User = _currentUser,
            Token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        };
    }

    public Task LogoutAsync()
    {
        _currentUser = null;
        return Task.CompletedTask;
    }

    public bool HasPermission(Permission permission)
    {
        var user = _currentUser;
        if (user == null)
        {
            return permission == Permission.None;
        }

        if (user.Role is UserRole.Admin or UserRole.SuperAdmin)
        {
            return true;
        }

        var custom = user.Permissions?.Aggregate(Permission.None, (current, item) => current | item)
            ?? Permission.None;
        return (GetRoleDefaultPermissions(user.Role) | custom).HasFlag(permission);
    }

    public bool HasRole(UserRole role)
    {
        var user = _currentUser;
        if (user == null)
        {
            return false;
        }

        return role is UserRole.Admin or UserRole.SuperAdmin
            ? user.Role is UserRole.Admin or UserRole.SuperAdmin
            : user.Role == role;
    }

    public Task<IEnumerable<UserInfo>> GetAllUsersAsync()
    {
        ThrowIfDisposed();
        if (!HasPermission(Permission.UserManagement))
        {
            return Task.FromResult<IEnumerable<UserInfo>>(Array.Empty<UserInfo>());
        }

        return Task.FromResult<IEnumerable<UserInfo>>(
            _users.Values.Select(ConvertToUserInfo).OrderBy(user => user.Username).ToList());
    }

    public async Task<bool> CreateUserAsync(CreateUserRequest request)
    {
        ThrowIfDisposed();
        if (!HasPermission(Permission.UserManagement) || !IsValidUsername(request.Username) ||
            !IsStrongPassword(request.Password))
        {
            return false;
        }

        var key = NormalizeUsername(request.Username);
        var user = new UserData
        {
            Username = request.Username.Trim(),
            DisplayName = request.DisplayName.Trim(),
            Email = request.Email.Trim(),
            PasswordHash = HashPassword(request.Password),
            Role = request.Role,
            CustomPermissions = request.CustomPermissions?.ToList() ?? new List<Permission>(),
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        if (!_users.TryAdd(key, user))
        {
            return false;
        }

        try
        {
            await SaveUsersAsync().ConfigureAwait(false);
            return true;
        }
        catch
        {
            _users.TryRemove(key, out _);
            return false;
        }
    }

    public async Task<bool> UpdateUserPermissionsAsync(
        string username,
        UserRole role,
        List<Permission> permissions)
    {
        ThrowIfDisposed();
        if (!HasPermission(Permission.UserManagement) ||
            !_users.TryGetValue(NormalizeUsername(username), out var user))
        {
            return false;
        }

        var oldRole = user.Role;
        var oldCustom = user.CustomPermissions.ToList();
        var oldPermissions = GetUserPermissions(oldRole, oldCustom);
        user.Role = role;
        user.CustomPermissions = permissions?.ToList() ?? new List<Permission>();

        try
        {
            await SaveUsersAsync().ConfigureAwait(false);
        }
        catch
        {
            user.Role = oldRole;
            user.CustomPermissions = oldCustom;
            return false;
        }

        var info = ConvertToUserInfo(user);
        if (string.Equals(_currentUser?.Username, user.Username, StringComparison.OrdinalIgnoreCase))
        {
            _currentUser = info;
        }

        PermissionChanged?.Invoke(this, new PermissionChangedEventArgs(
            info,
            oldPermissions,
            GetUserPermissions(user.Role, user.CustomPermissions)));
        return true;
    }

    public async Task<bool> SetUserActiveAsync(string username, bool isActive)
    {
        ThrowIfDisposed();
        if (!HasPermission(Permission.UserManagement) ||
            !_users.TryGetValue(NormalizeUsername(username), out var user) ||
            string.Equals(_currentUser?.Username, user.Username, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var previous = user.IsActive;
        user.IsActive = isActive;
        try
        {
            await SaveUsersAsync().ConfigureAwait(false);
            return true;
        }
        catch
        {
            user.IsActive = previous;
            return false;
        }
    }

    public async Task<bool> DeleteUserAsync(string username)
    {
        ThrowIfDisposed();
        var key = NormalizeUsername(username);
        if (!HasPermission(Permission.UserManagement) ||
            string.Equals(_currentUser?.Username, username, StringComparison.OrdinalIgnoreCase) ||
            !_users.TryGetValue(key, out var user))
        {
            return false;
        }

        if (user.Role == UserRole.SuperAdmin &&
            _users.Values.Count(item => item.Role == UserRole.SuperAdmin && item.IsActive) <= 1)
        {
            return false;
        }

        if (!_users.TryRemove(key, out var removed))
        {
            return false;
        }

        try
        {
            await SaveUsersAsync().ConfigureAwait(false);
            return true;
        }
        catch
        {
            _users[key] = removed;
            return false;
        }
    }

    private void LoadUsersSync()
    {
        if (!File.Exists(_usersFilePath))
        {
            return;
        }

        var json = File.ReadAllText(_usersFilePath, Encoding.UTF8);
        var users = JsonSerializer.Deserialize<Dictionary<string, UserData>>(json)
            ?? throw new InvalidDataException("The user data file is empty or invalid.");
        foreach (var pair in users)
        {
            if (!string.IsNullOrWhiteSpace(pair.Value.Username))
            {
                _users[NormalizeUsername(pair.Value.Username)] = pair.Value;
            }
        }
    }

    private async Task SaveUsersAsync()
    {
        await _fileLock.WaitAsync().ConfigureAwait(false);
        var temporaryPath = _usersFilePath + ".tmp";
        try
        {
            var snapshot = _users.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
            var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(temporaryPath, json, Encoding.UTF8).ConfigureAwait(false);
            File.Move(temporaryPath, _usersFilePath, overwrite: true);
        }
        finally
        {
            TryDelete(temporaryPath);
            _fileLock.Release();
        }
    }

    private static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            PasswordIterations,
            HashAlgorithmName.SHA256,
            HashSize);
        return $"PBKDF2-SHA256${PasswordIterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    private static bool VerifyPassword(string password, string stored, out bool needsRehash)
    {
        needsRehash = false;
        var parts = stored.Split('$');
        if (parts.Length == 4 && string.Equals(parts[0], "PBKDF2-SHA256", StringComparison.Ordinal) &&
            int.TryParse(parts[1], out var iterations))
        {
            try
            {
                var salt = Convert.FromBase64String(parts[2]);
                var expected = Convert.FromBase64String(parts[3]);
                var actual = Rfc2898DeriveBytes.Pbkdf2(
                    password,
                    salt,
                    iterations,
                    HashAlgorithmName.SHA256,
                    expected.Length);
                needsRehash = iterations < PasswordIterations;
                return CryptographicOperations.FixedTimeEquals(actual, expected);
            }
            catch (FormatException)
            {
                return false;
            }
        }

        // One-time compatibility for legacy deterministic SHA-256 hashes.
        try
        {
            var expected = Convert.FromBase64String(stored);
            var actual = SHA256.HashData(Encoding.UTF8.GetBytes(password + "UTF_SALT_2024"));
            needsRehash = true;
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static Permission GetRoleDefaultPermissions(UserRole role) => role switch
    {
        UserRole.SuperAdmin => Permission.AllPermissions,
        UserRole.Admin => Permission.AdminPermissions,
        UserRole.Engineer => Permission.EngineerPermissions,
        UserRole.Technician => Permission.TechnicianPermissions,
        UserRole.Operator => Permission.OperatorPermissions,
        UserRole.Observer => Permission.ObserverPermissions,
        _ => Permission.None
    };

    private static Permission GetUserPermissions(UserRole role, IEnumerable<Permission> custom) =>
        custom.Aggregate(GetRoleDefaultPermissions(role), (current, item) => current | item);

    private static UserInfo ConvertToUserInfo(UserData user) => new()
    {
        Username = user.Username,
        DisplayName = user.DisplayName,
        Email = user.Email,
        Role = user.Role,
        Permissions = user.CustomPermissions.ToList(),
        CreatedAt = user.CreatedAt,
        LastLoginAt = user.LastLoginAt,
        IsActive = user.IsActive
    };

    private static bool IsValidUsername(string username) =>
        !string.IsNullOrWhiteSpace(username) && username.Trim().Length is >= 3 and <= 64 &&
        username.All(character => char.IsLetterOrDigit(character) || character is '_' or '-' or '.');

    private static bool IsStrongPassword(string password) =>
        !string.IsNullOrEmpty(password) && password.Length >= 12;

    private static string NormalizeUsername(string? username) => username?.Trim().ToLowerInvariant() ?? string.Empty;

    private static LoginResult FailedLogin(string message) => new() { Success = false, Message = message };

    private static void RegisterFailedAttempt(LoginAttemptState state)
    {
        lock (state)
        {
            state.FailedCount++;
            if (state.FailedCount >= MaxFailedAttempts)
            {
                state.FailedCount = 0;
                state.LockedUntilUtc = DateTime.UtcNow.Add(LockoutDuration);
            }
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _fileLock.Dispose();
        _disposed = true;
    }

    private sealed class LoginAttemptState
    {
        public int FailedCount { get; set; }
        public DateTime LockedUntilUtc { get; set; }
    }
}

internal sealed class UserData
{
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Operator;
    public List<Permission> CustomPermissions { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastLoginAt { get; set; } = DateTime.MinValue;
    public bool IsActive { get; set; } = true;
}
