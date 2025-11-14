# Azure Developer CLI - .NET 10 Conversion

## Build Instructions

### Prerequisites
- .NET 10 SDK or later
- Azure CLI (for Bicep support)
- Git

### Quick Start

```bash
# Clone and navigate
cd cli/azd-dotnet

# Restore dependencies
dotnet restore

# Build all projects
dotnet build

# Run the CLI
cd src/azd
dotnet run -- --help
```

### Building Release Binaries

```bash
# Linux
dotnet publish src/azd/azd.csproj -c Release -r linux-x64 --self-contained -o ./bin/linux-x64

# Windows
dotnet publish src/azd/azd.csproj -c Release -r win-x64 --self-contained -o ./bin/win-x64

# macOS (Intel)
dotnet publish src/azd/azd.csproj -c Release -r osx-x64 --self-contained -o ./bin/osx-x64

# macOS (Apple Silicon)
dotnet publish src/azd/azd.csproj -c Release -r osx-arm64 --self-contained -o ./bin/osx-arm64
```

### Running Tests

```bash
# Run all tests
dotnet test

# Run with detailed output
dotnet test --logger "console;verbosity=detailed"

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"
```

### Development Workflow

```bash
# Watch mode (auto-rebuild on changes)
dotnet watch --project src/azd/azd.csproj run

# Format code
dotnet format

# Check for issues
dotnet build --no-incremental
```

## Project Status

This conversion is **IN PROGRESS**. See [README.md](README.md) for full details.

### What Works
- Project compiles (with some package reference fixes needed)
- Command structure is defined
- Core architecture is in place

### What Doesn't Work Yet
- Most command implementations (stubs only)
- Infrastructure provisioning
- Service deployment
- Template system
- Full Azure integration

## Next Steps

See the "What Still Needs To Be Done" section in [README.md](README.md).
