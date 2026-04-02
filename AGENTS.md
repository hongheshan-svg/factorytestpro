# Project Guidelines

## Project Structure
- `UniversalTestFramework.sln` is a .NET 10 solution for a Windows-only WPF test platform.
- Main projects: `UTF.HAL`, `UTF.Logging`, `UTF.Configuration`, `UTF.Core`, `UTF.Plugin.Abstractions`, `UTF.Plugin.Host`, `UTF.Business`, `UTF.Reporting`, `UTF.Vision`, `UTF.UI`, `UTF.Plugins.Drivers`, `UTF.Plugins.Example`, `tests/UTF.Core.Tests`.
- `UTF.UI` is the desktop entry point (`net10.0-windows`). Startup and DI composition live in `UTF.UI/App.xaml.cs`.
- Runtime configuration is centered on `config/unified-config.json`. Example and user data live under `Data/` and `UTF.UI/Data/`.
- Do not edit generated outputs in `bin/`, `obj/`, or runtime logs under `UTF.UI/bin/**/logs/` unless the task explicitly targets build artifacts.

## Architecture
- Keep business and execution logic out of the UI layer. Prefer placing reusable logic in `UTF.Core` or `UTF.Business`; `UTF.UI` should orchestrate windows, binding, and service wiring.
- Configuration-driven execution is the default flow: `config/unified-config.json` -> `UTF.UI.Services.ConfigurationManager` -> `ConfigDrivenTestOrchestrator` -> `ConfigDrivenTestEngine`.
- Plugin-based step execution goes through `UTF.Plugin.Host.StepExecutorPluginHost` and `IPluginService`. Plugin manifests are discovered from `plugins/<pluginId>/<version>/plugin.manifest.json` after build packaging.
- Dependency injection patterns are defined in `UTF.Core/DependencyInjection/ServiceCollectionExtensions.cs`, `UTF.Configuration/ServiceCollectionExtensions.cs`, and `UTF.UI/DependencyInjection/ServiceCollectionExtensions.cs`.
- Good reference files:
  - `UTF.UI/App.xaml.cs` for startup, DI, and crash handling
  - `UTF.Core/ConfigDrivenTestEngine.cs` for step execution behavior
  - `UTF.Core/ConfigDrivenTestOrchestrator.cs` for session orchestration and concurrency
  - `UTF.Plugin.Host/StepExecutorPluginHost.cs` for plugin loading and dispatch
  - `tests/UTF.Core.Tests/ConfigDrivenTestEngineTests.cs` for xUnit test style

## Build And Test
- Restore/build:
  - `dotnet restore UniversalTestFramework.sln`
  - `dotnet build UniversalTestFramework.sln -c Debug`
  - `dotnet build UniversalTestFramework.sln -c Release`
- Run the UI:
  - `dotnet run --project UTF.UI/UTF.UI.csproj -c Debug`
  - In VS Code, prefer the existing task `Run UTF.UI` when you need the app running.
- Tests:
  - `dotnet test`
  - `dotnet test tests/UTF.Core.Tests/UTF.Core.Tests.csproj --logger "console;verbosity=detailed"`
- Validation scripts:
  - `./verify-migration.sh`
  - `.\verify-migration.ps1`
- `UTF.UI/UTF.UI.csproj` automatically runs `scripts/pack-plugins.ps1` after build, so plugin packaging should usually be validated by a normal solution build rather than a separate manual copy step.

## Conventions
- Use C# with 4-space indentation, Allman braces, nullable-aware code, and explicit null handling.
- Follow existing naming patterns: PascalCase for types and members, `_camelCase` for private fields, and namespace names matching folders.
- Prefer constructor injection and interface-based dependencies when extending services.
- Public APIs should have XML documentation when introducing new reusable types or interfaces.
- Tests use xUnit and should follow `Method_Scenario_ExpectedResult` naming with clear Arrange/Act/Assert structure.
- This codebase is Windows-specific. Avoid proposing cross-platform runtime changes unless the task explicitly asks for them.
- Configuration-driven steps support fields such as `TargetDeviceId`, `RetryCount`, `StoreResultAs`, and `ConditionExpression`, and command or expected templates using `{{key}}` or `${key}`. Preserve these behaviors when changing execution logic.
- Result validation is prefix-driven (`contains:`, `equals:`, `regex:`, `notcontains:`). Keep new validation behavior compatible with the existing config format.

## Reference Docs
- See `config/README.md` for configuration structure and step field details.
- See `plugins/README.md` for plugin manifest fields, layout, and packaging.
- See `tests/README.md` for the broader test plan and planned test project split.
- See `docs/migration-guide.md` for config-driven migration guidance.
- See `docs/completeness-check.md` and `docs/implementation-complete.md` for verification status and implemented scope.
- See `docs/architecture-optimization-report.md` for architecture background and prior design decisions.

## Agent Guidance
- Prefer editing source files over generated assets.
- When adding project-wide behavior, update the corresponding documentation link target rather than duplicating large explanations here.
- If a task is specific to one area, consider adding scoped instructions under `.github/instructions/` instead of expanding this file.