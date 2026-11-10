# NuNuGet

A simple, standalone, opinionated NuGet client for scripts to install packages - and their dependencies - to the global packages folder.

## Overview

NuNuGet is a lightweight command-line tool that restores NuGet packages based on a simple JSON configuration file. It's designed for scenarios where you need to manage package dependencies outside of a traditional .NET project structure, such as native C++ projects that consume NuGet packages. It is not intended - or designed - to be run interactively, it is designed for build-systems to use to populate dependencies.

## Features

- **JSON-based package list** - Define packages in a simple JSON format instead of `.csproj` files
- **Lock file support** - Generate and use lock files for reproducible builds
- **Custom NuGet configuration** - Use a specific `nuget.config` for package sources
- **Cross-platform** - Supports Windows (x64, ARM64), Linux (x64), and macOS (ARM64)
- **Single-file deployment** - Can be published as a self-contained single executable

## Installation

Build from source:

```powershell
dotnet build
```

Or publish as a single executable (see [Publishing](#publishing) section below).

## Usage

### Install Command

Install packages from a package list JSON file:

```powershell
NuNuGet install --listFile <path> --configFile <path> --lockFile <path> [--verbose]
```

#### Options

| Option            | Required  | Description                                                           |
|-------------------|-----------|-----------------------------------------------------------------------|
| `--configFile`    | Yes       | Path to the NuGet configuration file to use                           |
| `--listFile`      | Yes       | Path to the `packages.list.json` file to restore                      |
| `--lockFile`      | Yes       | Path to the NuGet lock file to use                                    |
| `--verbose`       | No        | Enable verbose output                                                 |

### Example

```powershell
NuNuGet install --configFile ./nuget.config --listFile ./packages.list.json --lockFile ./packages.lock.json --verbose
```

## Package List Format

The package list is a JSON file that specifies the target framework and packages to install:

```json
{
  "targetFramework": "net8.0",
  "packages": [
    {
      "id": "Newtonsoft.Json",
      "version": "13.0.3"
    },
    {
      "id": "Microsoft.Extensions.Logging",
      "version": "8.0.0"
    }
  ]
}
```

### Schema

| Property              | Type      | Description                                                               |
|-----------------------|-----------|---------------------------------------------------------------------------|
| `targetFramework`     | string    | The target framework moniker (e.g., `native`, `net8.0`, `netstandard2.0`). `any` and `native` probe common managed fallbacks during restore so framework-specific packages can still resolve. |
| `packages`            | array     | List of package entries                                                   |
| `packages[].id`       | string    | The NuGet package identifier                                              |
| `packages[].version`  | string    | The version string (e.g., `1.8.2109`, `[1.0,2.0)`, `1.*`)                 |

## Publishing

Build single-file executables for distribution:

```powershell
# Framework-dependent single file
dotnet publish .\NuNuGet.csproj /p:PublishProfile=SingleFile

# Framework-dependent, trimmed
dotnet publish .\NuNuGet.csproj /p:PublishProfile=SingleFileTrimmed

# Self-contained, trimmed (Windows x64)
dotnet publish .\NuNuGet.csproj --runtime win-x64 /p:PublishProfile=SingleFileSelfContainedTrimmed

# Self-contained, trimmed (Windows ARM64)
dotnet publish .\NuNuGet.csproj --runtime win-arm64 /p:PublishProfile=SingleFileSelfContainedTrimmed
```

### Publish Profiles

| Profile                             | Description                                                                           |
|-------------------------------------|---------------------------------------------------------------------------------------|
| `SelfContained`                     | Self-contained (no .NET runtime required)                                             |
| `SingleFile`                        | Framework-dependent single file                                                       |
| `SingleFileSelfContained`           | Self-contained single file (no .NET runtime required)                                 |
| `SingleFileTrimmed`                 | Framework-dependent with IL trimming                                                  |
| `SingleFileSelfContainedTrimmed`    | Self-contained with IL trimming (no .NET runtime required)                            |
| `SingleFileSelfContainedTrimmedAot` | Self-contained with IL trimming (no .NET runtime required) and AOT compiled           |

## Packaging

```powershell
dotnet pack --configuration Release /bl
```

## Dependencies

NuNuGet uses:

- the official NuGet Client SDK libraries:
  - `NuGet.Commands` - Restore command infrastructure
  - `NuGet.Common` - Common types and utilities
  - `NuGet.Packaging` - Package reading and extraction
  - `NuGet.ProjectModel` - Project model types
  - `NuGet.Resolver` - Dependency resolution

- `System.CommandLine` - for command-line parsing
- `Microsoft.Extensions.Logging` - for common logging
