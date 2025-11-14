#!/bin/bash
# Build script for azd .NET conversion

set -e

echo "Building Azure Developer CLI (.NET 10)"
echo "======================================"

# Check for .NET SDK
if ! command -v dotnet &> /dev/null; then
    echo "Error: .NET SDK not found. Please install .NET 10 SDK or later."
    exit 1
fi

# Display .NET version
echo "Using .NET SDK:"
dotnet --version
echo

# Restore dependencies
echo "Restoring NuGet packages..."
dotnet restore
echo

# Build solution
echo "Building solution..."
dotnet build --no-restore --configuration Release
echo

# Run tests
echo "Running tests..."
dotnet test --no-build --configuration Release --verbosity normal
echo

# Publish binaries
echo "Publishing binaries..."
dotnet publish src/azd/azd.csproj \
    --configuration Release \
    --runtime linux-x64 \
    --self-contained \
    --output ./bin/linux-x64 \
    /p:PublishSingleFile=true \
    /p:PublishTrimmed=true

echo
echo "Build complete! Binary location: ./bin/linux-x64/azd"
echo
echo "To build for other platforms:"
echo "  Windows: dotnet publish -r win-x64 ..."
echo "  macOS:   dotnet publish -r osx-arm64 ..."
