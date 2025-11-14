# Build script for azd .NET conversion (PowerShell)

Write-Host "Building Azure Developer CLI (.NET 10)" -ForegroundColor Green
Write-Host "======================================" -ForegroundColor Green

# Check for .NET SDK
if (!(Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Host "Error: .NET SDK not found. Please install .NET 10 SDK or later." -ForegroundColor Red
    exit 1
}

# Display .NET version
Write-Host "Using .NET SDK:"
dotnet --version
Write-Host

# Restore dependencies
Write-Host "Restoring NuGet packages..." -ForegroundColor Cyan
dotnet restore
Write-Host

# Build solution
Write-Host "Building solution..." -ForegroundColor Cyan
dotnet build --no-restore --configuration Release
Write-Host

# Run tests
Write-Host "Running tests..." -ForegroundColor Cyan
dotnet test --no-build --configuration Release --verbosity normal
Write-Host

# Publish binaries
Write-Host "Publishing binaries..." -ForegroundColor Cyan
dotnet publish src/azd/azd.csproj `
    --configuration Release `
    --runtime win-x64 `
    --self-contained `
    --output ./bin/win-x64 `
    /p:PublishSingleFile=true `
    /p:PublishTrimmed=true

Write-Host
Write-Host "Build complete! Binary location: ./bin/win-x64/azd.exe" -ForegroundColor Green
Write-Host
Write-Host "To build for other platforms:"
Write-Host "  Linux: dotnet publish -r linux-x64 ..."
Write-Host "  macOS: dotnet publish -r osx-arm64 ..."
