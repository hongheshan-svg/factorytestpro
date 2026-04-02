# Universal Test Framework

Universal Test Framework is a Windows-only automated test platform for configuration-driven DUT testing.
It is designed for high-throughput factory scenarios, supports parallel testing for 16 or more DUTs, and allows custom test content through configuration and plugins.

Its goal is to help factories expand their automation testing capability, scale test coverage faster, and protect product quality with repeatable, configurable, and extensible test workflows.

Factories that want to expand their automation testing capability are welcome to contact hongheshan@gmail.com.
Contributions are also welcome from anyone who wants to help improve the codebase, extend test capabilities, or refine the platform for broader factory use.

## Overview

- Platform: WPF on .NET 10 (`net10.0-windows`)
- Core model: configuration-driven execution from `config/unified-config.json`
- Extensibility: step execution through plugins discovered from `plugins/<pluginId>/<version>/plugin.manifest.json`
- Main layers: `UTF.Core`, `UTF.Configuration`, `UTF.Plugin.Host`, `UTF.Business`, `UTF.UI`

## Key Capabilities

- Configuration-driven test projects and DUT setup
- Parallel orchestration for 16 or more DUTs in the same test session
- Plugin-based step execution by step type and channel
- Custom test content can be defined without changing core code, including commands, channels, validation rules, conditional execution, retry policies, and plugin-backed step types
- Parallel DUT orchestration
- Reporting and validation pipeline
- Driver plugins for serial, telnet, SCPI, and ADB scenarios

## Scalability And Customization

- Multi-DUT execution is part of the core design, and the framework targets production stations that need to run more than 16 DUTs concurrently.
- Test content is not limited to built-in templates. You can define device-specific or product-specific steps in configuration, route them to different channels, and extend execution behavior through plugins.
- Supported customization includes step sequencing, expected-result matching, retry control, context variables, conditional execution, target device selection, and custom plugin step handlers.
- For configuration details, see `config/unified-config.json` and `config/README.md`.
- For plugin packaging and routing rules, see `plugins/README.md`.

## Project Structure

- `UTF.Core`: orchestration, execution, validation, persistence, events
- `UTF.Configuration`: configuration models, serializers, validators
- `UTF.Plugin.Abstractions`: plugin contracts and shared metadata
- `UTF.Plugin.Host`: plugin loading, isolation, dispatch
- `UTF.Business`: business coordination services
- `UTF.UI`: WPF desktop application
- `UTF.Plugins.Example`: example command executor plugin
- `UTF.Plugins.Drivers`: transport and device driver plugins
- `tests/UTF.Core.Tests`: xUnit test project for core and plugin-host behavior

## Build

```powershell
dotnet restore UniversalTestFramework.sln
dotnet build UniversalTestFramework.sln -c Debug
```

## Run

```powershell
dotnet run --project UTF.UI/UTF.UI.csproj -c Debug
```

## Test

```powershell
dotnet test tests/UTF.Core.Tests/UTF.Core.Tests.csproj --logger "console;verbosity=minimal"
```

## Configuration And Plugins

- Main runtime configuration: `config/unified-config.json`
- Configuration reference: `config/README.md`
- Plugin layout and manifest reference: `plugins/README.md`
- Test project notes: `tests/README.md`

## Current Status

- Solution builds successfully in Debug
- `UTF.Core.Tests` currently passes all tests in this workspace
- The app is Windows-specific and expects real device connectivity or plugin-backed execution for production use

## Notes

- The repository is published at `hongheshan-svg/factorytestpro` and the local `main` branch tracks `origin/main`.
- This repository is released under the MIT License. See `LICENSE` for the full text.
