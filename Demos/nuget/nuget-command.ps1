#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Demonstrates the new 'apilens nuget' command.

.DESCRIPTION
    Shows how to use the nuget command to automatically scan and index
    the NuGet package cache without manual path specification.

.EXAMPLE
    ./demo-nuget-command.ps1
#>

$ErrorActionPreference = "Stop"

Write-Host "`n🚀 ApiLens NuGet Command Demo" -ForegroundColor Cyan
Write-Host "================================" -ForegroundColor Cyan

# Build if needed
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$apilens = Join-Path $repoRoot "Solutions/ApiLens.Cli/bin/Debug/net9.0/apilens"
if (-not (Test-Path $apilens)) {
    Write-Host "`nBuilding ApiLens..." -ForegroundColor Yellow
    $csproj = Join-Path $repoRoot "Solutions/ApiLens.Cli/ApiLens.Cli.csproj"
    dotnet build $csproj --verbosity quiet
}

# Demo 1: List packages in cache
Write-Host "`n1️⃣ List all packages with XML documentation:" -ForegroundColor Green
Write-Host "$ apilens nuget --list" -ForegroundColor Yellow
& $apilens nuget --list | Select-Object -First 20
Write-Host "... (truncated)" -ForegroundColor DarkGray

# Demo 2: List filtered packages
Write-Host "`n2️⃣ List packages matching pattern:" -ForegroundColor Green
Write-Host "$ apilens nuget --list --filter `"newtonsoft.*`"" -ForegroundColor Yellow
& $apilens nuget --list --filter "newtonsoft.*"

# Demo 3: Index all packages (clean index)
$tmpBase = Join-Path ([System.IO.Path]::GetTempPath()) "apilens-demo"
$indexPath = Join-Path $tmpBase "indexes/nuget-demo-index"
Write-Host "`n3️⃣ Index all NuGet packages:" -ForegroundColor Green
Write-Host "$ apilens nuget --clean --index $indexPath" -ForegroundColor Yellow
& $apilens nuget --clean --index $indexPath

# Demo 4: Query the indexed packages
Write-Host "`n4️⃣ Query indexed packages:" -ForegroundColor Green
Write-Host "$ apilens query JsonSerializer --index $indexPath" -ForegroundColor Yellow
& $apilens query JsonSerializer --index $indexPath --max 5

# Demo 5: Index only latest versions
Write-Host "`n5️⃣ Index only latest versions:" -ForegroundColor Green
Write-Host "$ apilens nuget --clean --latest-only --index ./nuget-latest-index" -ForegroundColor Yellow
& $apilens nuget --clean --latest-only --index ./nuget-latest-index

# Compare statistics
Write-Host "`n6️⃣ Compare index sizes:" -ForegroundColor Green
Write-Host "`nAll versions index:" -ForegroundColor Yellow
& $apilens stats --index $indexPath --format json | ConvertFrom-Json | Select-Object documentCount, totalSizeInBytes

Write-Host "`nLatest versions only:" -ForegroundColor Yellow
& $apilens stats --index ./nuget-latest-index --format json | ConvertFrom-Json | Select-Object documentCount, totalSizeInBytes

# Demo 7: Filter and index specific packages
Write-Host "`n7️⃣ Index only Microsoft packages (latest versions):" -ForegroundColor Green
Write-Host "$ apilens nuget --clean --filter `"microsoft.*`" --latest-only --index ./ms-index" -ForegroundColor Yellow
& $apilens nuget --clean --filter "microsoft.*" --latest-only --index ./ms-index

# Cleanup
Write-Host "`n🧹 Cleaning up demo indexes..." -ForegroundColor Yellow
Remove-Item $indexPath -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item ./nuget-latest-index -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item ./ms-index -Recurse -Force -ErrorAction SilentlyContinue

Write-Host "`n✅ Demo Complete!" -ForegroundColor Green
Write-Host @"

Key Features of 'apilens nuget' command:
- Automatically discovers NuGet cache location
- Scans all packages with XML documentation
- Supports regex filtering (--filter)
- Can index only latest versions (--latest-only)
- Progress tracking for large caches
- Cross-platform support via Spectre.IO

Usage examples:
  apilens nuget                           # Index all packages
  apilens nuget --latest-only             # Only latest versions
  apilens nuget --filter "system.*"       # Filter by pattern
  apilens nuget --list                    # List without indexing
  apilens nuget --clean --latest-only     # Clean index, latest only
"@ -ForegroundColor Cyan