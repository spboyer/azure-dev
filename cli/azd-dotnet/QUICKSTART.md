# .NET 10 Azure Developer CLI Conversion

## Overview

This directory contains the initial conversion of the Azure Developer CLI from Go to .NET 10.

**Status:** 🚧 **WORK IN PROGRESS** - Initial scaffolding complete, implementations needed.

## Quick Links

- [Full Documentation](README.md) - Complete conversion documentation
- [Build Instructions](BUILD.md) - How to build and run
- [Go Implementation](../azd/) - Original Go codebase for reference

## What's Included

✅ Complete project structure  
✅ Command framework with System.CommandLine  
✅ Core service interfaces  
✅ Infrastructure provider abstractions  
✅ Authentication integration  
✅ Test project setup  

🚧 Command implementations (stubs)  
🚧 Azure SDK integration (partial)  
🚧 Service deployment logic  
🚧 Template system  

## Quick Start

```bash
cd cli/azd-dotnet
dotnet build
cd src/azd
dotnet run -- version
```

## Architecture

```
azd (executable)
  ├── azd.Core (domain models & interfaces)
  ├── azd.Infra (infrastructure providers)
  └── azd.Auth (Azure authentication)
```

## Key Technologies

- **.NET 10** with Native AOT support
- **System.CommandLine** for CLI framework
- **Azure SDK for .NET** for Azure integration
- **xUnit** for testing
- **OpenTelemetry** for observability

## Estimated Completion

Full conversion: **14-21 weeks** (see [README.md](README.md) for details)

This initial phase represents approximately **10-15%** of the total conversion effort.

## License

Copyright (c) Microsoft Corporation. All rights reserved.
Licensed under the MIT License.
