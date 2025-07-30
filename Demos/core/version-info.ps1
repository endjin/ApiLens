#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Quick demonstration of NuGet cache indexing with version info.

.DESCRIPTION
    A simple script showing the key feature: indexing NuGet packages
    and querying them with version information displayed.

.EXAMPLE
    ./demo-quick.ps1
#>

$ErrorActionPreference = "Stop"

# Colors
function Write-Step { Write-Host "`n→ $args" -ForegroundColor Green }
function Write-Cmd { Write-Host "`n$ $args" -ForegroundColor Yellow }

Write-Host "`n🚀 ApiLens NuGet Version Demo" -ForegroundColor Cyan
Write-Host "================================" -ForegroundColor Cyan

# Build if needed
$apilens = "./Solutions/ApiLens.Cli/bin/Debug/net9.0/apilens"
if (-not (Test-Path $apilens)) {
    Write-Step "Building ApiLens..."
    dotnet build ./Solutions/ApiLens.Cli/ApiLens.Cli.csproj --verbosity quiet
}

# Ensure we have a package to demo
Write-Step "Ensuring sample package in NuGet cache..."
$tempProj = Join-Path ([System.IO.Path]::GetTempPath()) "demo-$(Get-Random).csproj"
@"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup>
  <ItemGroup><PackageReference Include="Newtonsoft.Json" Version="13.0.3" /></ItemGroup>
</Project>
"@ > $tempProj
dotnet restore $tempProj --verbosity quiet
Remove-Item $tempProj

# Demo paths
$tmpBase = Join-Path ([System.IO.Path]::GetTempPath()) "apilens-demo"
$indexPath = Join-Path $tmpBase "indexes/demo-index"
$nugetCache = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { "$HOME/.nuget/packages" }

# Clean previous demo
if (Test-Path $indexPath) { Remove-Item $indexPath -Recurse -Force }

Write-Step "Indexing Newtonsoft.Json from NuGet cache..."
Write-Cmd "apilens index `"$nugetCache/newtonsoft.json`" --clean --pattern `"**/*.xml`""
& $apilens index "$nugetCache/newtonsoft.json" --index $indexPath --clean --pattern "**/*.xml"

Write-Step "Querying with version info (Table Format):"
Write-Cmd "apilens query JsonSerializer"
& $apilens query JsonSerializer --index $indexPath

Write-Step "Querying with version info (JSON Format):"
Write-Cmd "apilens query JsonConvert --format json"
$jsonOutput = & $apilens query JsonConvert --index $indexPath --format json
if ($jsonOutput -and $jsonOutput -ne "No results found.") {
    $json = $jsonOutput | ConvertFrom-Json
    $json | Select-Object name, packageVersion, targetFramework | Format-Table
} else {
    Write-Host "  No results found for JsonConvert" -ForegroundColor Yellow
}

Write-Step "Querying with version info (Markdown Format):"
Write-Cmd "apilens query JObject --format markdown"
& $apilens query JObject --index $indexPath --format markdown --max 1

Write-Step "Index Statistics:"
Write-Cmd "apilens stats"
& $apilens stats --index $indexPath

# Cleanup
Remove-Item $indexPath -Recurse -Force

Write-Host "`n✅ Demo Complete!" -ForegroundColor Green
Write-Host @"

Key Features Demonstrated:
- Indexed packages from NuGet cache location
- Version information displayed in query results  
- Multiple output formats with version details
- Cross-platform path handling via Spectre.IO

The Version column shows: {version} [{framework}]
"@ -ForegroundColor Cyan