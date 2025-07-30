#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Demonstrates version comparison features in ApiLens.

.DESCRIPTION
    Shows how to compare APIs across different versions of packages
    and find the latest versions in your NuGet cache.

.EXAMPLE
    ./demo-version-comparison.ps1
#>

param(
    [string]$Package = "newtonsoft.json",
    [string]$IndexPath = "/.tmp/indexes/version-demo-index"
)

$ErrorActionPreference = "Stop"

# Set up paths
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$apilens = Join-Path $repoRoot "Solutions/ApiLens.Cli/bin/Debug/net9.0/apilens"
if ($IsWindows) {
    $apilens = "$apilens.exe"
}

# Ensure built
if (-not (Test-Path $apilens)) {
    Write-Host "Building ApiLens..." -ForegroundColor Yellow
    $csproj = Join-Path $repoRoot "Solutions/ApiLens.Cli/ApiLens.Cli.csproj"
    dotnet build $csproj --verbosity quiet
}

# Clean index
if (Test-Path $IndexPath) {
    Remove-Item $IndexPath -Recurse -Force
}

# Get NuGet cache path
$nugetCache = if ($env:NUGET_PACKAGES) {
    $env:NUGET_PACKAGES
} else {
    Join-Path $HOME ".nuget/packages"
}

Write-Host "`n=== ApiLens Version Comparison Demo ===" -ForegroundColor Cyan
Write-Host "Package: $Package" -ForegroundColor Cyan

# Check if package exists in cache
$packagePath = Join-Path $nugetCache $Package
if (-not (Test-Path $packagePath)) {
    Write-Warning "Package '$Package' not found in NuGet cache at $packagePath"
    Write-Host "Try: dotnet add package $Package"
    exit 1
}

# Show available versions
Write-Host "`nAvailable versions in cache:" -ForegroundColor Green
$versions = Get-ChildItem -Path $packagePath -Directory | Sort-Object Name
$versions | ForEach-Object { Write-Host "  - $($_.Name)" }

# Index all versions
Write-Host "`nIndexing all versions..." -ForegroundColor Yellow
& $apilens index $packagePath --index $IndexPath --pattern "**/*.xml" | Out-Null

# Query and show version differences
Write-Host "`n1. All JsonSerializer classes across versions:" -ForegroundColor Green
& $apilens query JsonSerializer --index $IndexPath --format table

Write-Host "`n2. Version details in JSON format:" -ForegroundColor Green
$jsonOutput = & $apilens query JsonSerializer --index $IndexPath --format json
if ($jsonOutput -and $jsonOutput -ne "No results found.") {
    $results = $jsonOutput | ConvertFrom-Json
    $results | Select-Object name, packageVersion, targetFramework | Format-Table
} else {
    Write-Host "  No results found" -ForegroundColor Yellow
    exit 1
}

Write-Host "`n3. APIs grouped by version:" -ForegroundColor Green
$grouped = $results | Group-Object packageVersion
foreach ($group in $grouped) {
    Write-Host "`n  Version $($group.Name): $($group.Count) APIs"
    $group.Group | Select-Object name, targetFramework -First 3 | Format-Table -AutoSize
}

Write-Host "`n4. Find latest version APIs only:" -ForegroundColor Green
$latestVersion = $versions | Select-Object -Last 1 -ExpandProperty Name
Write-Host "  Latest version: $latestVersion"
$latestApis = $results | Where-Object { $_.packageVersion -eq $latestVersion }
Write-Host "  Found $($latestApis.Count) APIs in latest version"

Write-Host "`n5. Framework distribution:" -ForegroundColor Green
$results | Group-Object targetFramework | ForEach-Object {
    Write-Host "  $($_.Name): $($_.Count) APIs"
}

# Cleanup
Write-Host "`nCleaning up..." -ForegroundColor Yellow
Remove-Item $IndexPath -Recurse -Force

Write-Host "`nDemo complete!" -ForegroundColor Green
Write-Host "This demonstrated how ApiLens tracks version information for NuGet packages." -ForegroundColor Cyan