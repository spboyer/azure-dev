# Azure Developer CLI - .NET 10 Conversion

This directory contains the .NET 10 conversion of the Azure Developer CLI (azd), originally written in Go.

## 🚧 Project Status: INITIAL CONVERSION

This is a foundational conversion that establishes the .NET 10 project structure and core architecture. **This is NOT production-ready** and represents only the initial scaffolding of the full conversion effort.

## Project Structure

```
azd-dotnet/
├── AzureDeveloperCli.sln        # Solution file
├── global.json                   # .NET SDK version specification
├── Directory.Build.props         # Common MSBuild properties
├── src/
│   ├── azd/                      # Main CLI executable
│   │   ├── azd.csproj
│   │   ├── Program.cs           # Entry point with System.CommandLine
│   │   ├── Commands/            # Command handlers
│   │   ├── Services/            # Core services (version, updates)
│   │   └── Telemetry/           # Telemetry implementation
│   ├── azd.Core/                # Core domain models and services
│   │   ├── azd.Core.csproj
│   │   ├── Models/              # ProjectConfig, Environment, etc.
│   │   └── Services/            # IProjectService, etc.
│   ├── azd.Infra/               # Infrastructure provisioning
│   │   ├── azd.Infra.csproj
│   │   └── Provisioning/        # Bicep, Terraform providers
│   └── azd.Auth/                # Azure authentication
│       ├── azd.Auth.csproj
│       └── AuthService.cs       # Azure Identity integration
└── tests/
    └── azd.Tests/               # xUnit tests
        ├── azd.Tests.csproj
        └── VersionServiceTests.cs
```

## Architecture Mapping: Go → .NET

### Command Line Framework
- **Go**: Cobra (github.com/spf13/cobra)
- **.NET**: System.CommandLine (System.CommandLine 2.0 beta)

### Dependency Injection
- **Go**: Custom IoC container (pkg/ioc)
- **.NET**: Microsoft.Extensions.DependencyInjection (built-in)

### Configuration
- **Go**: Viper + custom config
- **.NET**: Microsoft.Extensions.Configuration with JSON/Environment providers

### Azure SDK
- **Go**: Azure SDK for Go
- **.NET**: Azure SDK for .NET
  - Azure.Identity for authentication
  - Azure.ResourceManager for ARM operations
  - Specific Azure.ResourceManager.* packages for each service

### Logging
- **Go**: Standard log package + custom telemetry
- **.NET**: Microsoft.Extensions.Logging + Application Insights

### Telemetry & Tracing
- **Go**: OpenTelemetry + custom Application Insights integration
- **.NET**: OpenTelemetry.NET + Microsoft.ApplicationInsights

### Testing
- **Go**: testify + table-driven tests
- **.NET**: xUnit + Moq + FluentAssertions

## Key Dependencies

### Main CLI (azd.csproj)
- `System.CommandLine` - Command-line parsing
- `Microsoft.Extensions.Hosting` - Application host
- `Microsoft.Extensions.DependencyInjection` - DI container
- `Microsoft.ApplicationInsights` - Telemetry
- `OpenTelemetry` - Distributed tracing

### Core Library (azd.Core.csproj)
- `Azure.Core` - Azure SDK core
- `Azure.Identity` - Authentication
- `System.Text.Json` - JSON serialization
- `YamlDotNet` - YAML parsing (for azure.yaml)

### Infrastructure (azd.Infra.csproj)
- `Azure.ResourceManager` - Azure Resource Manager
- `Azure.ResourceManager.Resources` - ARM deployments
- `Azure.ResourceManager.AppService` - App Service management
- `Azure.ResourceManager.AppContainers` - Container Apps
- `Azure.ResourceManager.Storage` - Storage accounts
- And many more Azure service-specific packages

### Authentication (azd.Auth.csproj)
- `Azure.Identity` - Azure authentication
- `Microsoft.Identity.Client` - MSAL integration

## What Has Been Implemented

✅ **Project Structure**
- Solution and project files
- Directory structure matching Go architecture
- Build configuration (Directory.Build.props)
- .NET 10 targeting

✅ **Command Framework**
- Root command with global options (--cwd, --debug, --output)
- Command structure for all major commands:
  - init, up, down, deploy, provision
  - auth (login, logout, token)
  - config, env, infra, monitor, pipeline
  - version
- Command handler stubs

✅ **Core Services**
- Version service
- Update check service (with caching logic)
- Telemetry service (stub)

✅ **Domain Models**
- ProjectConfig (azure.yaml representation)
- ServiceConfig, DockerConfig, HooksConfig
- Environment model
- IProjectService interface

✅ **Infrastructure**
- IInfrastructureProvider interface
- BicepProvider (partial implementation)
- Provisioning models (ProvisionOptions, ProvisionResult, etc.)

✅ **Authentication**
- IAuthService interface
- AuthService with Azure Identity integration
- Support for DefaultAzureCredential chain

✅ **Testing Infrastructure**
- xUnit test project
- Sample version service tests
- FluentAssertions + Moq setup

## What Still Needs To Be Done

### Immediate Next Steps

1. **Fix Compilation Errors**
   - Add missing NuGet package references
   - Fix namespace resolution issues
   - Correct Azure.ResourceManager API usage

2. **Complete Core Implementations**
   - ProjectService (load/save azure.yaml)
   - Environment management
   - Configuration service
   - Template system

3. **Infrastructure Providers**
   - Complete Bicep provider implementation
   - Add Bicep CLI integration (call `az bicep build`)
   - Implement Terraform provider
   - ARM template deployment logic

4. **Command Implementations**
   - Convert all command handlers from Go
   - Port business logic from cmd/ directory
   - Implement progress reporting/spinners
   - Add proper error handling

5. **Service Detection**
   - Port appdetect package
   - Language/framework detection
   - Service configuration inference

6. **Deployment Logic**
   - Service packaging (Docker, zip files)
   - Azure deployment (App Service, Container Apps, AKS)
   - Connection string management
   - Environment variable injection

### Major Components Not Yet Converted

- **Project Lifecycle** (pkg/project/)
  - Service framework detection
  - Build and package logic
  - Deployment orchestration
  - Hooks execution

- **Azure Integration** (pkg/)
  - Full ARM client integration
  - Resource graph queries
  - Azure DevOps integration
  - GitHub Actions integration

- **Templates** (pkg/templates/)
  - Template download and caching
  - Template instantiation
  - Custom template support

- **Environment Management**
  - Environment creation/selection
  - Secret management
  - .env file handling

- **Configuration**
  - Config file management
  - User settings
  - Workspace settings

- **Extensions**
  - Extension loading
  - gRPC extension server
  - Hook system

- **CI/CD Pipeline**
  - Pipeline configuration generation
  - GitHub Actions workflow creation
  - Azure DevOps pipeline creation

- **Monitoring**
  - Application Insights integration
  - Log streaming
  - Metrics queries

- **Tooling Integration**
  - VS Code extension compatibility
  - Azure DevOps task compatibility
  - Dev Container support

### Testing
- Port all unit tests from Go
- Port integration tests
- Add snapshot testing equivalent
- Set up CI/CD pipelines

### Documentation
- API documentation
- Migration guide from Go version
- Command reference
- Architecture documentation

## Building and Running

### Prerequisites
- .NET 10 SDK
- Azure CLI (for Bicep support)

### Build
```bash
cd cli/azd-dotnet
dotnet restore
dotnet build
```

### Run
```bash
cd src/azd
dotnet run -- version
dotnet run -- --help
```

### Test
```bash
dotnet test
```

### Publish (Single Executable)
```bash
dotnet publish src/azd/azd.csproj -c Release -r linux-x64 --self-contained
dotnet publish src/azd/azd.csproj -c Release -r win-x64 --self-contained
dotnet publish src/azd/azd.csproj -c Release -r osx-arm64 --self-contained
```

## Native AOT Compilation

The project is configured for Native AOT compilation (`<PublishAot>true</PublishAot>`), which produces:
- Smaller executables
- Faster startup times
- No .NET runtime dependency

To build Native AOT:
```bash
dotnet publish -c Release -r linux-x64
```

Note: Not all Azure SDK libraries are fully Native AOT compatible. You may need to disable AOT for some scenarios.

## Design Decisions

### Why System.CommandLine?
- Modern, officially supported command-line framework
- Better than CommandLineParser or custom parsing
- Good TypeScript/Cobra-like experience
- Native dependency injection support

### Why Azure.ResourceManager?
- New unified Azure SDK for .NET
- Consistent API across all Azure services
- Better than older Microsoft.Azure.Management.* packages
- Long-term support from Microsoft

### Why xUnit over NUnit?
- More modern and widely adopted in .NET ecosystem
- Better async test support
- Clean separation of test organization

### Why Separate Projects?
- Better separation of concerns
- Easier to test in isolation
- Allows for future library distribution
- Matches Go package structure

## Equivalent Go Packages

| Go Package | .NET Equivalent |
|-----------|----------------|
| `cli/azd/cmd` | `src/azd/Commands` |
| `cli/azd/pkg/ioc` | Built-in `Microsoft.Extensions.DependencyInjection` |
| `cli/azd/pkg/project` | `src/azd.Core/Services` |
| `cli/azd/pkg/infra` | `src/azd.Infra/Provisioning` |
| `cli/azd/pkg/auth` | `src/azd.Auth` |
| `cli/azd/internal/telemetry` | `src/azd/Telemetry` |
| `cli/azd/test` | `tests/azd.Tests` |

## Performance Considerations

### Go Advantages
- Smaller binary size (~40MB)
- Faster cold start
- Lower memory usage
- Native goroutines for concurrency

### .NET Advantages
- Rich ecosystem and libraries
- Better Azure SDK integration
- Easier async/await programming
- Native AOT can match Go performance
- Better IDE support and tooling

### Mitigation Strategies
- Use Native AOT compilation
- Lazy load dependencies
- Minimize reflection usage
- Use source generators where possible

## Migration Timeline Estimate

Based on the scope of the Go codebase:

- **Phase 1** (Completed): Project setup and architecture - **1-2 weeks**
- **Phase 2**: Core services and commands - **3-4 weeks**
- **Phase 3**: Infrastructure providers - **2-3 weeks**
- **Phase 4**: Deployment and service management - **3-4 weeks**
- **Phase 5**: Templates and extensions - **2-3 weeks**
- **Phase 6**: Testing and polish - **2-3 weeks**
- **Phase 7**: CI/CD and release - **1-2 weeks**

**Total Estimated Effort**: 14-21 weeks (3.5-5 months) for a full team

## Contributing

This conversion is a work in progress. When implementing new features:

1. Follow existing code structure and patterns
2. Port Go tests alongside Go code
3. Use async/await consistently
4. Add XML documentation comments
5. Follow .NET naming conventions
6. Run `dotnet format` before committing

## License

Copyright (c) Microsoft Corporation. All rights reserved.
Licensed under the MIT License.

## Original Go Implementation

The original Go implementation can be found at: `../azd/`

For comparison and reference when porting functionality.
