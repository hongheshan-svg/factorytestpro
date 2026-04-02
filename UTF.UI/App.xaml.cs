using System;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using UTF.Core;
using UTF.Core.DependencyInjection;
using UTF.Configuration;
using UTF.Configuration.Validators;
using UTF.UI.DependencyInjection;

namespace UTF.UI;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private IServiceProvider? _serviceProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        try
        {
            // 设置全局异常处理
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            DispatcherUnhandledException += App_DispatcherUnhandledException;

            base.OnStartup(e);

            // 配置依赖注入
            var services = new ServiceCollection();
            ConfigureServices(services);
            _serviceProvider = services.BuildServiceProvider();

            // 启动时验证配置
            ValidateConfiguration(_serviceProvider);

            // 从 DI 容器获取主窗口
            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();

            // 设置主窗口为应用程序的主窗口
            this.MainWindow = mainWindow;
            mainWindow.Show();
            mainWindow.Activate();
        }
        catch (Exception ex)
        {
            var crashLog = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs", "startup-crash.log");
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(crashLog)!);
            System.IO.File.WriteAllText(crashLog, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 启动崩溃: {ex}\n");
            MessageBox.Show($"应用程序启动错误: {ex.Message}\n\n详情已写入: {crashLog}", "启动错误", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // 注册 UTF 核心服务
        services.AddUtfCore();

        // 注册 UTF 配置服务（验证器、序列化器）
        services.AddUtfConfiguration();

        // 注册 UTF 业务服务
        services.AddUtfBusiness();

        // 注册 UTF UI 服务
        services.AddUtfUI();

        // 注册 UI 窗口（瞬态，每次打开新窗口都创建新实例）
        services.AddTransient<MainWindow>();
        services.AddTransient<TestPlanEditorWindow>();
        services.AddTransient<DUTTestListWindow>();
        services.AddTransient<ConfigurationCenterWindow>();
        services.AddTransient<QuickTestWizardWindow>();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // 释放 DI 容器
        if (_serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }

        base.OnExit(e);
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var exception = e.ExceptionObject as Exception;
        WriteCrashLog($"CurrentDomain_UnhandledException: {exception}");
        MessageBox.Show($"未处理的异常: {exception?.Message}", "严重错误", MessageBoxButton.OK, MessageBoxImage.Error);
    }
    
    private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        WriteCrashLog($"DispatcherUnhandledException: {e.Exception}");
        MessageBox.Show($"界面异常: {e.Exception.Message}", "界面错误", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true; // 防止应用程序崩溃
    }

    private static void WriteCrashLog(string message)
    {
        try
        {
            var logDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            System.IO.Directory.CreateDirectory(logDir);
            var logFile = System.IO.Path.Combine(logDir, "startup-crash.log");
            System.IO.File.AppendAllText(logFile, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}\n");
        }
        catch { /* 忽略日志写入失败 */ }
    }

    /// <summary>
    /// 启动时验证配置，如有错误则警告用户但不阻塞启动
    /// </summary>
    private void ValidateConfiguration(IServiceProvider serviceProvider)
    {
        try
        {
            var validator = serviceProvider.GetService<CompositeConfigurationValidator>();
            if (validator == null) return;

            var configManager = serviceProvider.GetService<UTF.UI.Services.ConfigurationManager>();
            if (configManager == null) return;

            var config = Task.Run(() => configManager.GetUnifiedConfigurationAsync()).GetAwaiter().GetResult();

            var systemConfig = new UTF.Configuration.Models.SystemConfig
            {
                LogLevel = config.SystemSettings?.LogLevel ?? "",
                DefaultLanguage = config.SystemSettings?.DefaultLanguage ?? "",
                ResultsPath = config.SystemSettings?.ResultsPath ?? "./test-results",
                Theme = config.SystemSettings?.Theme ?? "Light"
            };

            var dutConfig = new UTF.Configuration.Models.DUTConfig
            {
                ProductName = config.DUTConfiguration?.ProductInfo?.Name ?? "",
                ProductModel = config.DUTConfiguration?.ProductInfo?.Model ?? "",
                MaxConcurrent = config.DUTConfiguration?.GlobalSettings?.DefaultMaxConcurrent ?? 16
            };

            var testConfig = new UTF.Configuration.Models.TestConfig
            {
                ProjectId = config.TestProjectConfiguration?.TestProject?.Id ?? "",
                ProjectName = config.TestProjectConfiguration?.TestProject?.Name ?? ""
            };

            if (config.TestProjectConfiguration?.TestProject?.Steps != null)
            {
                foreach (var step in config.TestProjectConfiguration.TestProject.Steps)
                {
                    testConfig.Steps.Add(new UTF.Configuration.Models.TestStepConfig
                    {
                        Id = step.Id ?? "",
                        Name = step.Name ?? "",
                        Type = step.Type ?? "",
                        Command = step.Command ?? "",
                        Expected = step.Expected,
                        Timeout = step.Timeout ?? 30000,
                        Enabled = step.Enabled
                    });
                }
            }

            var result = validator.ValidateAll(systemConfig, dutConfig, testConfig);

            if (!result.IsValid)
            {
                var logger = serviceProvider.GetService<UTF.Logging.ILogger>();
                foreach (var error in result.AllErrors)
                {
                    logger?.Warning($"配置验证: {error}");
                }
            }
        }
        catch (Exception ex)
        {
            // 配置验证失败不应阻塞启动
            System.Diagnostics.Debug.WriteLine($"配置验证异常: {ex.Message}");
        }
    }
}

