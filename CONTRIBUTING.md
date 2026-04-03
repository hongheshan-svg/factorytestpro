# Contributing

Thank you for helping improve Universal Test Framework.

## Ways To Contribute

- Report bugs in configuration-driven execution, plugin loading, UI workflows, or DUT orchestration.
- Propose improvements for factory scalability, test coverage, and product-quality safeguards.
- Submit code changes for new drivers, plugins, validators, reports, and documentation.
- Improve examples, onboarding docs, and production deployment guidance.

## Before You Start

- Read [README.md](README.md) for project scope and repository status.
- Read [AGENTS.md](AGENTS.md) for project structure, architecture boundaries, and build/test commands.
- For configuration behavior, see [config/README.md](config/README.md).
- For plugin layout and manifests, see [plugins/README.md](plugins/README.md).
- For test scope, see [tests/README.md](tests/README.md).

## Development Expectations

- Keep reusable business, execution, validation, and orchestration logic out of `UTF.UI`.
- Preserve the config-driven flow from `config/unified-config.json` through `ConfigurationManager`, `ConfigDrivenTestOrchestrator`, and `ConfigDrivenTestEngine`.
- Keep plugin-based execution routed through `UTF.Plugin.Host.StepExecutorPluginHost` and `IPluginService`.
- Preserve existing step semantics such as `TargetDeviceId`, `RetryCount`, `StoreResultAs`, `ConditionExpression`, `{{key}}`, `${key}`, and validation prefixes like `contains:` and `regex:`.
- Keep changes focused. Do not mix unrelated refactors into one PR.

## Build And Test

Use these commands before opening a pull request:

```powershell
dotnet restore UniversalTestFramework.sln
dotnet build UniversalTestFramework.sln -c Debug
dotnet test tests/UTF.Core.Tests/UTF.Core.Tests.csproj --logger "console;verbosity=minimal"
```

If your change affects the WPF app or plugin packaging, also validate:

```powershell
dotnet run --project UTF.UI/UTF.UI.csproj -c Debug
```

## Pull Request Guidelines

- Use a short, specific title.
- Describe the problem, the root-cause fix, and any behavior changes.
- Mention any config, plugin, or UI workflow impact.
- Include test evidence or explain why test coverage was not added.
- If screenshots or logs help explain UI or runtime behavior, include them.

## Reporting Bugs Or Factory Requirements

- For production or factory deployment collaboration, contact hongheshan@gmail.com.
- For open-source collaboration, open an issue or pull request in this repository.
