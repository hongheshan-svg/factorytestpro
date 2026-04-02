# Universal Test Framework

Universal Test Framework is a Windows-only automated test platform for configuration-driven DUT testing.

## Overview

- Platform: WPF on .NET 10 (`net10.0-windows`)
- Core model: configuration-driven execution from `config/unified-config.json`
- Extensibility: step execution through plugins discovered from `plugins/<pluginId>/<version>/plugin.manifest.json`
- Main layers: `UTF.Core`, `UTF.Configuration`, `UTF.Plugin.Host`, `UTF.Business`, `UTF.UI`

## Key Capabilities

- Configuration-driven test projects and DUT setup
- Plugin-based step execution by step type and channel
- Parallel DUT orchestration
- Reporting and validation pipeline
- Driver plugins for serial, telnet, SCPI, and ADB scenarios

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

- This repository currently has no Git remote configured in the workspace clone.
- If you want to publish it as a public GitHub repository, configure a remote and choose an OSS license before pushing.
