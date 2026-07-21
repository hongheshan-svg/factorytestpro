using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UTF.Configuration;
using UTF.Configuration.Models;

namespace UTF.Core.Configuration;

/// <summary>
/// Headless / non-UI loader for <c>unified-config.json</c>.
/// Wraps <see cref="UnifiedConfigurationManager"/> and implements <see cref="IConfigurationService"/>.
/// </summary>
public sealed class FileUnifiedConfigurationService : IConfigurationService, IDisposable
{
    private readonly UnifiedConfigurationManager _manager;

    public event EventHandler? ConfigurationChanged
    {
        add => _manager.ConfigurationChanged += value;
        remove => _manager.ConfigurationChanged -= value;
    }

    /// <param name="configPathOrDirectory">
    /// Path to a <c>unified-config.json</c> file, or a directory containing that file.
    /// </param>
    public FileUnifiedConfigurationService(string configPathOrDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configPathOrDirectory);
        _manager = new UnifiedConfigurationManager(
            new UnifiedConfigurationAdapter(),
            configPathOrDirectory);
    }

    public string ConfigPath => _manager.ConfigFilePath;

    public UnifiedConfigurationManager Manager => _manager;

    public async Task RefreshAsync()
    {
        await _manager.RefreshAsync().ConfigureAwait(false);
        // Force reload
        _ = await _manager.GetUnifiedConfigurationAsync().ConfigureAwait(false);
    }

    public async Task SaveConfigurationAsync(object config)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (config is not UnifiedConfiguration document)
        {
            throw new ArgumentException(
                $"Expected {nameof(UnifiedConfiguration)}, got {config.GetType().FullName}.",
                nameof(config));
        }

        await _manager.SaveUnifiedConfigurationAsync(document).ConfigureAwait(false);
    }

    public async Task<T?> GetConfigurationSectionAsync<T>(string section) where T : class
    {
        var unified = await _manager.GetUnifiedConfigurationAsync().ConfigureAwait(false);
        object? value = section switch
        {
            "TestProjectConfiguration" => unified.TestProjectConfiguration,
            "DUTConfiguration" => unified.DUTConfiguration,
            "SystemSettings" => unified.SystemSettings,
            "ConfigurationInfo" => unified.ConfigurationInfo,
            "UnifiedConfiguration" => unified,
            _ => null
        };

        return value as T;
    }

    /// <summary>
    /// Maps the loaded document into a <see cref="ConfigTestProject"/> for direct session creation.
    /// </summary>
    public async Task<ConfigTestProject?> ToConfigTestProjectAsync()
    {
        var unified = await _manager.GetUnifiedConfigurationAsync().ConfigureAwait(false);
        var project = unified.TestProjectConfiguration?.TestProject;
        if (project == null || string.IsNullOrWhiteSpace(project.Id))
        {
            return null;
        }

        var defaultRetry = unified.DUTConfiguration?.GlobalSettings?.RetryCount ?? 0;
        return new ConfigTestProject
        {
            Id = project.Id,
            Name = project.Name ?? string.Empty,
            Description = project.Description ?? string.Empty,
            Enabled = project.Enabled,
            Steps = project.Steps.Select(step => new ConfigTestStep
            {
                Id = string.IsNullOrWhiteSpace(step.Id) ? Guid.NewGuid().ToString() : step.Id,
                Name = step.Name ?? string.Empty,
                Description = step.Description ?? string.Empty,
                Order = step.Order,
                Enabled = step.Enabled,
                Type = step.Type,
                TargetDeviceId = step.TargetDeviceId ?? step.Target,
                Command = step.Command,
                Expected = step.Expected,
                Timeout = step.Timeout,
                Delay = step.Delay,
                RetryCount = step.RetryCount ?? defaultRetry,
                Channel = step.Channel,
                StoreResultAs = step.StoreResultAs,
                ConditionExpression = step.ConditionExpression,
                ContinueOnFailure = step.ContinueOnFailure,
                ValidationRules = step.ValidationRules == null
                    ? null
                    : new Dictionary<string, object>(step.ValidationRules),
                Parameters = step.Parameters == null
                    ? null
                    : new Dictionary<string, object>(step.Parameters)
            }).ToList()
        };
    }

    /// <summary>Synchronous convenience for tests that already loaded config.</summary>
    public ConfigTestProject? ToConfigTestProject()
    {
        return ToConfigTestProjectAsync().GetAwaiter().GetResult();
    }

    public async Task<DUTConfigInfo> ToDutConfigInfoAsync()
    {
        var unified = await _manager.GetUnifiedConfigurationAsync().ConfigureAwait(false);
        var dut = unified.DUTConfiguration;
        return new DUTConfigInfo
        {
            ProductName = dut?.ProductInfo?.Name ?? string.Empty,
            ProductModel = dut?.ProductInfo?.Model ?? string.Empty,
            ExpectedSoftwareVersion = dut?.ProductInfo?.ExpectedSoftwareVersion ?? string.Empty,
            DefaultMaxConcurrent = dut?.GlobalSettings?.DefaultMaxConcurrent ?? 16,
            TestTimeout = dut?.GlobalSettings?.TestTimeout ?? 300,
            RetryCount = dut?.GlobalSettings?.RetryCount ?? 3,
            SerialPorts = EndpointMapper.GetSerialAddresses(unified),
            NetworkHosts = EndpointMapper.GetNetworkAddresses(unified)
        };
    }

    public DUTConfigInfo ToDutConfigInfo()
    {
        return ToDutConfigInfoAsync().GetAwaiter().GetResult();
    }

    public void Dispose() => _manager.Dispose();
}
