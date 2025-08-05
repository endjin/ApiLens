#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Simple demo showing the new 'apilens nuget' command.

.DESCRIPTION
    Quick demonstration of how easy it is to index NuGet packages now.

.EXAMPLE
    ./demo-nuget-simple.ps1
#>

$ErrorActionPreference = "Stop"

Write-Host "`n🚀 ApiLens NuGet Command - Simple Demo" -ForegroundColor Cyan
Write-Host "=======================================" -ForegroundColor Cyan

# Build if needed
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$apilens = Join-Path $repoRoot "Solutions/ApiLens.Cli/bin/Debug/net9.0/apilens"
if ($IsWindows -or $env:OS -eq "Windows_NT") { 
    $apilens += ".exe" 
}
if (-not (Test-Path $apilens)) {
    Write-Host "`nBuilding ApiLens..." -ForegroundColor Yellow
    $csproj = Join-Path $repoRoot "Solutions/ApiLens.Cli/ApiLens.Cli.csproj"
    dotnet build $csproj --verbosity quiet
}

Write-Host "`n✨ OLD WAY (manual indexing):" -ForegroundColor Red
$examplePath = Join-Path "`$HOME" ".nuget" "packages" "newtonsoft.json"
Write-Host "$ apilens index `"$examplePath`" --pattern `"**/*.xml`"" -ForegroundColor DarkGray
Write-Host "  ❌ Need to know cache location" -ForegroundColor DarkGray
Write-Host "  ❌ Need to specify recursive pattern" -ForegroundColor DarkGray
Write-Host "  ❌ Index one package at a time" -ForegroundColor DarkGray

Write-Host "`n✨ NEW WAY (nuget command):" -ForegroundColor Green
Write-Host '$ apilens nuget --filter "newtonsoft.*"' -ForegroundColor Yellow
Write-Host "  ✅ Auto-discovers cache location" -ForegroundColor Green
Write-Host "  ✅ Handles all the complexity" -ForegroundColor Green
Write-Host "  ✅ Can index multiple packages at once" -ForegroundColor Green

Write-Host "`n📦 Let's try it:" -ForegroundColor Cyan

# Clean index
$tmpBase = Join-Path ([System.IO.Path]::GetTempPath()) "apilens-demo"
$indexPath = Join-Path $tmpBase "indexes/demo-nuget-index"
if (Test-Path $indexPath) { Remove-Item $indexPath -Recurse -Force }

# Index Newtonsoft packages
Write-Host "`n$ apilens nuget --filter `"newtonsoft.*`" --latest-only" -ForegroundColor Yellow
& "$apilens" nuget --filter "newtonsoft.*" --latest-only --index "$indexPath"

# Query the results
Write-Host "`n🔍 Now query the indexed packages:" -ForegroundColor Cyan
Write-Host "$ apilens query JsonSerializer" -ForegroundColor Yellow
& "$apilens" query JsonSerializer --index "$indexPath"

# Show version info in different formats
Write-Host "`n📊 Version info in JSON format:" -ForegroundColor Cyan
Write-Host "$ apilens query JObject --format json | Select-Object name, packageVersion, targetFramework" -ForegroundColor Yellow
try {
    $jsonOutput = & "$apilens" query JObject --index "$indexPath" --format json --max 3
    if ($jsonOutput -and $jsonOutput -ne "[]" -and $jsonOutput -notmatch "No results found") {
        $json = $jsonOutput | ConvertFrom-Json
        $json | Select-Object name, packageVersion, targetFramework | Format-Table
    } else {
        Write-Host "No JObject results found" -ForegroundColor Yellow
    }
} catch {
    Write-Host "Error processing JSON output: $_" -ForegroundColor Red
}

# Cleanup
Remove-Item $indexPath -Recurse -Force

Write-Host "`n✅ That's it! Much simpler with 'apilens nuget' command!" -ForegroundColor Green