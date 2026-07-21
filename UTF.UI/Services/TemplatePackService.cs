using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using UTF.UI.Models;

namespace UTF.UI.Services;

/// <summary>
/// Scans <c>config/templates</c> for process packs and applies them as the active unified config.
/// </summary>
public sealed class TemplatePackService : ITemplatePackService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private readonly ConfigurationManager _configurationManager;
    private readonly UTF.Logging.ILogger? _logger;
    private readonly string _templatesDirectory;

    /// <summary>
    /// Creates the service. Templates are resolved from
    /// <c>AppDomain.BaseDirectory/config/templates</c>, with a walk-up fallback
    /// to a solution-relative <c>config/templates</c> for local dev.
    /// </summary>
    public TemplatePackService(
        ConfigurationManager configurationManager,
        UTF.Logging.ILogger? logger = null,
        string? templatesDirectoryOverride = null)
    {
        _configurationManager = configurationManager
            ?? throw new ArgumentNullException(nameof(configurationManager));
        _logger = logger;
        _templatesDirectory = string.IsNullOrWhiteSpace(templatesDirectoryOverride)
            ? ResolveTemplatesDirectory()
            : Path.GetFullPath(templatesDirectoryOverride);
    }

    /// <inheritdoc />
    public string TemplatesDirectory => _templatesDirectory;

    /// <inheritdoc />
    public IReadOnlyList<TemplatePackInfo> GetAvailablePacks()
    {
        if (string.IsNullOrWhiteSpace(_templatesDirectory) || !Directory.Exists(_templatesDirectory))
        {
            _logger?.Warning($"模板目录不存在: {_templatesDirectory}");
            return Array.Empty<TemplatePackInfo>();
        }

        var packs = new List<TemplatePackInfo>();
        foreach (var file in Directory.EnumerateFiles(_templatesDirectory, "*.json", SearchOption.TopDirectoryOnly)
                     .OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                packs.Add(ReadPackInfo(file));
            }
            catch (Exception ex)
            {
                _logger?.Warning($"跳过无法解析的模板: {file} — {ex.Message}");
            }
        }

        return packs;
    }

    /// <inheritdoc />
    public async Task<UnifiedConfiguration> LoadPackAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("模板路径不能为空。", nameof(path));
        }

        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"模板文件不存在: {fullPath}", fullPath);
        }

        await using var stream = File.OpenRead(fullPath);
        var config = await JsonSerializer
            .DeserializeAsync<UnifiedConfiguration>(stream, JsonOptions, cancellationToken)
            .ConfigureAwait(false);

        if (config is null)
        {
            throw new InvalidDataException($"模板反序列化失败（空结果）: {fullPath}");
        }

        return config;
    }

    /// <inheritdoc />
    public async Task<string?> ApplyPackAsync(
        string path,
        bool backupCurrent = true,
        CancellationToken cancellationToken = default)
    {
        var config = await LoadPackAsync(path, cancellationToken).ConfigureAwait(false);

        string? backupPath = null;
        if (backupCurrent)
        {
            backupPath = await TryBackupCurrentConfigAsync(cancellationToken).ConfigureAwait(false);
        }

        await _configurationManager.SaveUnifiedConfigurationAsync(config).ConfigureAwait(false);
        await _configurationManager.RefreshConfiguration().ConfigureAwait(false);

        _logger?.Info(
            $"已应用工艺包模板: {Path.GetFileName(path)}" +
            (backupPath is null ? string.Empty : $"（备份: {backupPath}）"));

        return backupPath;
    }

    private async Task<string?> TryBackupCurrentConfigAsync(CancellationToken cancellationToken)
    {
        try
        {
            var configFilePath = _configurationManager.Inner.ConfigFilePath;
            if (!File.Exists(configFilePath))
            {
                return null;
            }

            var directory = Path.GetDirectoryName(configFilePath) ?? ".";
            var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var backupPath = Path.Combine(directory, $"unified-config.backup.{stamp}.json");
            await using (var source = File.OpenRead(configFilePath))
            await using (var dest = File.Create(backupPath))
            {
                await source.CopyToAsync(dest, cancellationToken).ConfigureAwait(false);
            }

            return backupPath;
        }
        catch (Exception ex)
        {
            _logger?.Warning($"备份当前配置失败（将继续应用模板）: {ex.Message}");
            return null;
        }
    }

    private static TemplatePackInfo ReadPackInfo(string fullPath)
    {
        var json = File.ReadAllText(fullPath);
        using var doc = JsonDocument.Parse(json, new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        });

        var root = doc.RootElement;
        var displayName = GetString(root, "ConfigurationInfo", "Name")
                          ?? Path.GetFileNameWithoutExtension(fullPath);
        var description = GetString(root, "ConfigurationInfo", "Description") ?? string.Empty;
        var productName = GetString(root, "DUTConfiguration", "ProductInfo", "Name");
        var productModel = GetString(root, "DUTConfiguration", "ProductInfo", "Model");
        var stepCount = CountSteps(root);
        var fileName = Path.GetFileName(fullPath);
        var (industry, tags) = InferIndustryAndTags(fileName);

        return new TemplatePackInfo
        {
            FileName = fileName,
            FullPath = fullPath,
            DisplayName = displayName,
            Description = description,
            Industry = industry,
            Tags = tags,
            ProductName = productName,
            ProductModel = productModel,
            StepCount = stepCount
        };
    }

    private static int CountSteps(JsonElement root)
    {
        if (!TryGetPropertyIgnoreCase(root, "TestProjectConfiguration", out var tpc))
        {
            return 0;
        }

        if (!TryGetPropertyIgnoreCase(tpc, "TestProject", out var project))
        {
            return 0;
        }

        if (!TryGetPropertyIgnoreCase(project, "Steps", out var steps) || steps.ValueKind != JsonValueKind.Array)
        {
            return 0;
        }

        return steps.GetArrayLength();
    }

    private static string? GetString(JsonElement root, params string[] path)
    {
        var current = root;
        foreach (var segment in path)
        {
            if (!TryGetPropertyIgnoreCase(current, segment, out current))
            {
                return null;
            }
        }

        return current.ValueKind == JsonValueKind.String ? current.GetString() : current.ToString();
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement element, string name, out JsonElement value)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            value = default;
            return false;
        }

        foreach (var prop in element.EnumerateObject())
        {
            if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = prop.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    /// <summary>
    /// Infer industry / tags from conventional template file names
    /// (e.g. <c>consumer-electronics-android.json</c> → industry=consumer-electronics).
    /// </summary>
    public static (string? Industry, IReadOnlyList<string> Tags) InferIndustryAndTags(string fileName)
    {
        var baseName = Path.GetFileNameWithoutExtension(fileName);
        if (string.IsNullOrWhiteSpace(baseName))
        {
            return (null, Array.Empty<string>());
        }

        var parts = baseName.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var tags = parts.ToArray();

        string? industry = null;
        if (parts.Length >= 2 &&
            (string.Equals(parts[0], "consumer", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(parts[0], "automotive", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(parts[0], "instrument", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(parts[0], "factory", StringComparison.OrdinalIgnoreCase)))
        {
            // consumer-electronics-*, automotive-*, instrument-integration-*, factory-*
            if (string.Equals(parts[0], "consumer", StringComparison.OrdinalIgnoreCase) &&
                parts.Length >= 2 &&
                string.Equals(parts[1], "electronics", StringComparison.OrdinalIgnoreCase))
            {
                industry = "consumer-electronics";
            }
            else if (string.Equals(parts[0], "instrument", StringComparison.OrdinalIgnoreCase) &&
                     parts.Length >= 2 &&
                     string.Equals(parts[1], "integration", StringComparison.OrdinalIgnoreCase))
            {
                industry = "instrument-integration";
            }
            else
            {
                industry = parts[0].ToLowerInvariant();
            }
        }

        return (industry, tags);
    }

    /// <summary>
    /// Prefer output-linked <c>BaseDirectory/config/templates</c>; walk up for
    /// solution-relative <c>config/templates</c> when running from bin/obj.
    /// </summary>
    internal static string ResolveTemplatesDirectory(string? startDirectory = null)
    {
        var baseDir = startDirectory ?? AppDomain.CurrentDomain.BaseDirectory;
        var candidates = new List<string>
        {
            Path.Combine(baseDir, "config", "templates")
        };

        var current = new DirectoryInfo(baseDir);
        for (var i = 0; i < 8 && current is not null; i++)
        {
            candidates.Add(Path.Combine(current.FullName, "config", "templates"));
            current = current.Parent;
        }

        foreach (var candidate in candidates)
        {
            if (Directory.Exists(candidate))
            {
                return Path.GetFullPath(candidate);
            }
        }

        // Default to BaseDirectory path even if missing (catalog will be empty).
        return Path.GetFullPath(Path.Combine(baseDir, "config", "templates"));
    }
}
