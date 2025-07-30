#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Demonstrates the NuGet cache scanning functionality via ApiLens.

.DESCRIPTION
    Shows how ApiLens discovers and processes packages in the local NuGet cache
    with cross-platform support.

.EXAMPLE
    ./nuget-scanner.ps1
#>

$ErrorActionPreference = "Stop"

Write-Host "`n=== NuGet Cache Scanner Demo ===" -ForegroundColor Cyan

# Build if needed
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$apilens = Join-Path $repoRoot "Solutions/ApiLens.Cli/bin/Debug/net9.0/apilens"
if (-not (Test-Path $apilens)) {
    Write-Host "`nBuilding ApiLens..." -ForegroundColor Yellow
    $csproj = Join-Path $repoRoot "Solutions/ApiLens.Cli/ApiLens.Cli.csproj"
    dotnet build $csproj --verbosity quiet
}

# Get NuGet cache location
Write-Host "`n📁 Discovering NuGet Cache Location..." -ForegroundColor Yellow
$nugetCache = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { "$HOME/.nuget/packages" }
Write-Host "NuGet Cache: $nugetCache" -ForegroundColor Cyan

# Create demo index
$indexPath = "/.tmp/indexes/scanner-demo-index"
if (Test-Path $indexPath) { Remove-Item $indexPath -Recurse -Force }

# Demo 1: List available packages
Write-Host "`n📦 Demo 1: List Available Packages in Cache" -ForegroundColor Green
Write-Host "Command: apilens nuget --list --filter 'json*'" -ForegroundColor Yellow
& $apilens nuget --list --filter "json*" | Select-Object -First 10

# Demo 2: Index specific packages
Write-Host "`n📦 Demo 2: Index Specific Packages" -ForegroundColor Green
Write-Host "Command: apilens nuget --filter 'newtonsoft.*' --latest --index $indexPath" -ForegroundColor Yellow
& $apilens nuget --filter "newtonsoft.*" --latest --index $indexPath

# Demo 3: Show index statistics
Write-Host "`n📊 Demo 3: Index Statistics" -ForegroundColor Green
Write-Host "Command: apilens stats --index $indexPath" -ForegroundColor Yellow
& $apilens stats --index $indexPath

# Demo 4: Query indexed packages
Write-Host "`n🔍 Demo 4: Query Indexed Packages" -ForegroundColor Green
Write-Host "Command: apilens query 'JsonSerializer' --index $indexPath" -ForegroundColor Yellow
& $apilens query "JsonSerializer" --index $indexPath | Select-Object -First 5

# Demo 5: Get JSON output for version info
Write-Host "`n📄 Demo 5: JSON Output with Version Info" -ForegroundColor Green
Write-Host "Command: apilens query 'JsonConvert' --format json --index $indexPath" -ForegroundColor Yellow
$jsonOutput = & $apilens query "JsonConvert" --format json --index $indexPath | Select-Object -First 100
if ($jsonOutput) {
    try {
        $parsed = $jsonOutput | ConvertFrom-Json
        if ($parsed -and $parsed.Count -gt 0) {
            Write-Host "`nParsed JSON - First result:" -ForegroundColor Cyan
            Write-Host "  Name: $($parsed[0].name)" -ForegroundColor Gray
            Write-Host "  Package: $($parsed[0].packageId)" -ForegroundColor Gray
            Write-Host "  Version: $($parsed[0].packageVersion)" -ForegroundColor Gray
            Write-Host "  Framework: $($parsed[0].targetFramework)" -ForegroundColor Gray
        }
    }
    catch {
        Write-Host "JSON sample displayed above" -ForegroundColor Gray
    }
}

# Demo 6: Multiple framework support
Write-Host "`n🎯 Demo 6: Multiple Framework Support" -ForegroundColor Green
Write-Host "Many NuGet packages target multiple frameworks:" -ForegroundColor Cyan
Write-Host "Command: apilens nuget --list --filter 'newtonsoft.json' --all" -ForegroundColor Yellow
& $apilens nuget --list --filter "newtonsoft.json" --all | Select-Object -First 15

# Cleanup
Write-Host "`n🧹 Cleanup" -ForegroundColor Yellow
if (Test-Path $indexPath) {
    Remove-Item $indexPath -Recurse -Force
    Write-Host "✅ Demo index removed" -ForegroundColor Green
}

Write-Host "`n✨ Demo complete!" -ForegroundColor Green
Write-Host @"

This demonstrated the NuGet cache scanning features:
- Cross-platform NuGet cache discovery
- Package listing and filtering
- Version-aware indexing
- Framework targeting information
- Latest version filtering
- Support for packages with multiple framework targets

The scanner works correctly on Windows, Linux, and macOS!
"@ -ForegroundColor Cyan