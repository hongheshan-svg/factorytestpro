using System.Text;
using System.Text.Json;
using UTF.Core;
using UTF.Core.Configuration;
using UTF.Core.Persistence;
using UTF.Logging;
using UTF.Plugin.Host;

namespace UTF.CLI;

/// <summary>
/// utf-run — headless config-driven test runner (Phase C).
/// Exit codes: 0 = all pass, 1 = one or more DUT failed, 2 = config/init error.
/// </summary>
internal static class Program
{
    private const int ExitPass = 0;
    private const int ExitFail = 1;
    private const int ExitError = 2;

    public static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        if (args.Length == 0 || HasFlag(args, "--help") || HasFlag(args, "-h") || HasFlag(args, "/?"))
        {
            PrintHelp();
            return args.Length == 0 ? ExitError : ExitPass;
        }

        CliOptions options;
        try
        {
            options = CliOptions.Parse(args);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Argument error: {ex.Message}");
            PrintHelp();
            return ExitError;
        }

        ILogger? logger = null;
        StepExecutorPluginHost? pluginHost = null;
        ConfigDrivenTestOrchestrator? orchestrator = null;

        try
        {
            logger = LoggerFactory.CreateLogger("utf-run", new LogConfiguration
            {
                MinLevel = LogLevel.Info,
                EnableConsole = true,
                EnableFile = true,
                LogFilePath = Path.Combine("logs", "utf-run.log")
            });

            logger.Info($"utf-run starting. config={options.ConfigPath}, plugins={options.PluginsDir}");

            FileUnifiedConfigurationService configService;
            try
            {
                configService = new FileUnifiedConfigurationService(options.ConfigPath);
                await configService.RefreshAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Config load failed: {ex.Message}");
                return ExitError;
            }

            var testProject = await configService.ToConfigTestProjectAsync().ConfigureAwait(false);
            if (testProject == null)
            {
                Console.Error.WriteLine("Config error: TestProjectConfiguration.TestProject is missing or has no Id.");
                return ExitError;
            }

            var dutConfig = await configService.ToDutConfigInfoAsync().ConfigureAwait(false);
            var dutIds = options.ResolveDutIds();
            if (dutIds.Count == 0)
            {
                Console.Error.WriteLine("No DUT IDs specified. Use --duts DUT-1,DUT-2 or --dut-count N.");
                return ExitError;
            }

            var unified = await configService.Manager.GetUnifiedConfigurationAsync().ConfigureAwait(false);
            var resultsPath = unified.SystemSettings?.ResultsPath;
            if (string.IsNullOrWhiteSpace(resultsPath))
            {
                resultsPath = "test-results";
            }

            Directory.CreateDirectory(resultsPath);

            // Optional plugins (MockOutput steps work without plugins).
            IPluginService? pluginService = null;
            if (Directory.Exists(options.PluginsDir))
            {
                pluginHost = new StepExecutorPluginHost(options.PluginsDir, logger);
                var report = await pluginHost.InitializeAsync().ConfigureAwait(false);
                logger.Info($"Plugins loaded={report.LoadedCount}, failed={report.FailedCount} from {options.PluginsDir}");
                foreach (var issue in report.Issues)
                {
                    logger.Warning($"  [{issue.ErrorCode}] {issue.ManifestPath}: {issue.Message}");
                }

                pluginService = new PluginServiceAdapter(pluginHost);
            }
            else
            {
                logger.Warning($"Plugins directory not found: {options.PluginsDir}. Continuing with built-in MockOutput only.");
            }

            var resultRepository = new FileTestResultRepository(resultsPath);
            var engine = new ConfigDrivenTestEngine(
                logger: logger,
                pluginService: pluginService,
                eventBus: null,
                resultRepository: resultRepository);

            orchestrator = new ConfigDrivenTestOrchestrator(configService, engine, logger);

            // Initialize validates config sections when present; project overload is used for session.
            var initialized = await orchestrator.InitializeAsync().ConfigureAwait(false);
            if (!initialized)
            {
                logger.Warning("Orchestrator InitializeAsync reported validation issues; continuing with explicit project.");
            }

            var session = await orchestrator.CreateSessionAsync(
                testProject,
                dutIds,
                operatorName: options.OperatorName,
                sessionContext: null,
                dutConfig: dutConfig).ConfigureAwait(false);

            if (session == null)
            {
                Console.Error.WriteLine("Failed to create test session (validation or DUT list error).");
                return ExitError;
            }

            Console.WriteLine($"Session {session.SessionId}");
            Console.WriteLine($"Project: {testProject.Name} ({testProject.Id})");
            Console.WriteLine($"Operator: {options.OperatorName}");
            Console.WriteLine($"DUTs ({dutIds.Count}): {string.Join(", ", dutIds)}");
            Console.WriteLine("Starting...");

            var started = await orchestrator.StartSessionAsync(session.SessionId).ConfigureAwait(false);
            if (!started)
            {
                Console.Error.WriteLine("Failed to start test session.");
                return ExitError;
            }

            try
            {
                await orchestrator.WaitForSessionAsync(session.SessionId).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Session wait failed: {ex.Message}");
                return ExitError;
            }

            var final = orchestrator.GetSession(session.SessionId);
            if (final == null)
            {
                Console.Error.WriteLine("Session disappeared after completion.");
                return ExitError;
            }

            var stats = orchestrator.GetSessionStatistics(session.SessionId);
            PrintSummary(final, stats);

            var summaryPath = await WriteSessionSummaryAsync(final, resultsPath).ConfigureAwait(false);
            Console.WriteLine($"Summary written: {summaryPath}");
            Console.WriteLine($"Per-DUT results directory: {Path.GetFullPath(resultsPath)}");

            await orchestrator.CleanupSessionAsync(session.SessionId).ConfigureAwait(false);

            if (final.Status is ConfigTestStatus.Error)
            {
                return ExitError;
            }

            return final.OverallPassed ? ExitPass : ExitFail;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Fatal: {ex.Message}");
            logger?.Error("utf-run fatal error", ex);
            return ExitError;
        }
        finally
        {
            orchestrator?.Dispose();
            if (pluginHost != null)
            {
                await pluginHost.DisposeAsync().ConfigureAwait(false);
            }

            if (logger is IDisposable disposableLogger)
            {
                disposableLogger.Dispose();
            }
        }
    }

    private static void PrintSummary(ConfigTestSession session, ConfigTestStatistics? stats)
    {
        Console.WriteLine();
        Console.WriteLine("========== Session Summary ==========");
        Console.WriteLine($"Status:  {session.Status}");
        Console.WriteLine($"Overall: {(session.OverallPassed ? "PASS" : "FAIL")}");
        if (!string.IsNullOrWhiteSpace(session.ErrorMessage))
        {
            Console.WriteLine($"Error:   {session.ErrorMessage}");
        }

        if (stats != null)
        {
            Console.WriteLine($"DUTs:    {stats.PassedDuts}/{stats.TotalDuts} passed ({stats.FailedDuts} failed)");
            Console.WriteLine($"Steps:   {stats.PassedSteps}/{stats.TotalSteps} passed");
            Console.WriteLine($"PassRate:{stats.PassRate:P1}");
            Console.WriteLine($"Duration:{stats.Duration.TotalSeconds:F2}s");
        }

        Console.WriteLine("---------- Per-DUT ----------");
        foreach (var dutId in session.DutIds)
        {
            if (session.DutResults.TryGetValue(dutId, out var report))
            {
                var flag = report.Passed ? "PASS" : "FAIL";
                var err = string.IsNullOrWhiteSpace(report.ErrorMessage) ? "" : $" — {report.ErrorMessage}";
                Console.WriteLine($"  [{flag}] {dutId}{err}");
            }
            else
            {
                Console.WriteLine($"  [MISS] {dutId}");
            }
        }

        Console.WriteLine("=====================================");
    }

    private static async Task<string> WriteSessionSummaryAsync(ConfigTestSession session, string resultsPath)
    {
        Directory.CreateDirectory(resultsPath);
        var fileName = $"session_{session.SessionId}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json";
        var path = Path.Combine(resultsPath, fileName);

        var payload = new
        {
            session.SessionId,
            ProjectId = session.TestProject.Id,
            ProjectName = session.TestProject.Name,
            session.Operator,
            Status = session.Status.ToString(),
            session.OverallPassed,
            session.ErrorMessage,
            session.StartTime,
            session.EndTime,
            DutResults = session.DutResults.ToDictionary(
                kv => kv.Key,
                kv => new
                {
                    kv.Value.Passed,
                    kv.Value.ErrorMessage,
                    Steps = kv.Value.StepResults.Select(s => new
                    {
                        s.StepId,
                        s.StepName,
                        s.Passed,
                        s.Skipped,
                        s.ErrorMessage
                    })
                })
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(path, json).ConfigureAwait(false);
        return Path.GetFullPath(path);
    }

    private static bool HasFlag(string[] args, string flag) =>
        args.Any(a => string.Equals(a, flag, StringComparison.OrdinalIgnoreCase));

    private static void PrintHelp()
    {
        Console.WriteLine("""
            utf-run — Universal Test Framework headless runner (Phase C)

            Usage:
              utf-run --config <path> [--duts DUT-1,DUT-2 | --dut-count N] [options]

            Required:
              --config <path>       Path to unified-config.json or a directory containing it

            DUT selection (one of):
              --duts <list>         Comma-separated DUT IDs (e.g. DUT-1,DUT-2)
              --dut-count <n>       Generate DUT-1 .. DUT-n

            Optional:
              --operator <name>     Operator name (default: cli)
              --plugins <dir>       Plugin root (default: ./plugins next to the executable)
              --help, -h            Show this help

            Exit codes:
              0  All DUTs passed
              1  One or more DUTs failed / session stopped
              2  Config or initialization error

            Plugins:
              Build UTF.UI to run scripts/pack-plugins.ps1, then either:
                utf-run --config config --plugins UTF.UI/bin/Debug/net10.0-windows/plugins
              or copy the packed plugins folder next to utf-run.exe.

            Phase C limitations:
              - No full PDF report generation (Reporting PDF still NotSupported)
              - Vision remains simulated
              - Real serial/instrument I/O requires matching plugins under --plugins
            """);
    }
}

internal sealed class CliOptions
{
    public string ConfigPath { get; init; } = "";
    public string PluginsDir { get; init; } = "";
    public string OperatorName { get; init; } = "cli";
    public IReadOnlyList<string>? ExplicitDuts { get; init; }
    public int? DutCount { get; init; }

    public List<string> ResolveDutIds()
    {
        if (ExplicitDuts is { Count: > 0 })
        {
            return ExplicitDuts.ToList();
        }

        if (DutCount is > 0)
        {
            return Enumerable.Range(1, DutCount.Value).Select(i => $"DUT-{i}").ToList();
        }

        return new List<string>();
    }

    public static CliOptions Parse(string[] args)
    {
        string? config = null;
        string? plugins = null;
        string? op = null;
        string? duts = null;
        int? count = null;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            string? Next()
            {
                if (i + 1 >= args.Length)
                {
                    throw new ArgumentException($"Missing value for {arg}");
                }

                return args[++i];
            }

            if (string.Equals(arg, "--config", StringComparison.OrdinalIgnoreCase))
            {
                config = Next();
            }
            else if (string.Equals(arg, "--plugins", StringComparison.OrdinalIgnoreCase))
            {
                plugins = Next();
            }
            else if (string.Equals(arg, "--operator", StringComparison.OrdinalIgnoreCase))
            {
                op = Next();
            }
            else if (string.Equals(arg, "--duts", StringComparison.OrdinalIgnoreCase))
            {
                duts = Next();
            }
            else if (string.Equals(arg, "--dut-count", StringComparison.OrdinalIgnoreCase))
            {
                if (!int.TryParse(Next(), out var n) || n <= 0)
                {
                    throw new ArgumentException("--dut-count must be a positive integer");
                }

                count = n;
            }
            else if (arg.StartsWith('-'))
            {
                throw new ArgumentException($"Unknown argument: {arg}");
            }
            else
            {
                throw new ArgumentException($"Unexpected positional argument: {arg}");
            }
        }

        if (string.IsNullOrWhiteSpace(config))
        {
            throw new ArgumentException("--config is required");
        }

        if (duts != null && count != null)
        {
            throw new ArgumentException("Specify either --duts or --dut-count, not both");
        }

        if (duts == null && count == null)
        {
            // Default single DUT for convenience in CI smoke runs
            count = 1;
        }

        var explicitList = duts?
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();

        return new CliOptions
        {
            ConfigPath = Path.GetFullPath(config),
            PluginsDir = Path.GetFullPath(plugins ?? Path.Combine(AppContext.BaseDirectory, "plugins")),
            OperatorName = string.IsNullOrWhiteSpace(op) ? "cli" : op.Trim(),
            ExplicitDuts = explicitList,
            DutCount = count
        };
    }
}
