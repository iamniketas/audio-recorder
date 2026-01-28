# Build and run AudioRecorder
# Usage:
#   .\build-and-run.ps1          # Debug build
#   .\build-and-run.ps1 Release  # Release build

param(
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"
$ProjectRoot = $PSScriptRoot

Write-Host "Building AudioRecorder ($Configuration)..." -ForegroundColor Cyan

# Build the solution
dotnet build "$ProjectRoot\AudioRecorder.sln" --configuration $Configuration

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

Write-Host "Build successful! Starting application..." -ForegroundColor Green

# Determine exe path based on configuration
$ExePath = "$ProjectRoot\src\AudioRecorder.App\bin\x86\$Configuration\net8.0-windows10.0.19041.0\win-x86\AudioRecorder.exe"

if (-not (Test-Path $ExePath)) {
    Write-Host "Executable not found at: $ExePath" -ForegroundColor Red
    exit 1
}

# Start the application
Start-Process $ExePath
