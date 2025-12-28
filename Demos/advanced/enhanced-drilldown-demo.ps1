#!/usr/bin/env pwsh
# Demo: Enhanced Drill-Down Experience in ApiLens
# This script demonstrates the new exploration capabilities

Write-Host "`n=== ApiLens Enhanced Drill-Down Demo ===" -ForegroundColor Cyan
Write-Host "Demonstrating the new package exploration and smart querying features`n" -ForegroundColor Gray

# Get the repository root (two levels up from Demos/advanced/)
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$apilens = Join-Path $repoRoot "Solutions/ApiLens.Cli/bin/Debug/net10.0/apilens"
if ($IsWindows) { $apilens += ".exe" }

# Step 1: Analyze a solution
Write-Host "Step 1: Analyzing the ApiLens solution..." -ForegroundColor Yellow
& $apilens analyze ./Solutions/ApiLens.sln --clean | Select-Object -Last 5
Start-Sleep -Seconds 2

# Step 2: Index a popular package
Write-Host "`nStep 2: Indexing Newtonsoft.Json from NuGet cache..." -ForegroundColor Yellow
& $apilens nuget --filter "newtonsoft.json" --latest-only | Select-Object -Last 3
Start-Sleep -Seconds 2

# Step 3: Explore the package interactively
Write-Host "`nStep 3: Exploring Newtonsoft.Json package structure..." -ForegroundColor Yellow
Write-Host "Command: apilens explore newtonsoft.json" -ForegroundColor Gray
& $apilens explore newtonsoft.json | Select-Object -First 40
Start-Sleep -Seconds 3

# Step 4: Smart querying with grouping
Write-Host "`nStep 4: Querying with category grouping..." -ForegroundColor Yellow
Write-Host "Command: apilens query 'Json' --type content --group-by membertype --max 15" -ForegroundColor Gray
& $apilens query "Json" --type content --group-by membertype --max 15 | Select-Object -First 30
Start-Sleep -Seconds 2

# Step 5: Finding well-documented APIs
Write-Host "`nStep 5: Finding well-documented Parse methods..." -ForegroundColor Yellow
Write-Host "Command: apilens query 'Parse' --type method --quality-first --max 5" -ForegroundColor Gray
& $apilens query "Parse" --type method --quality-first --max 5
Start-Sleep -Seconds 2

# Step 6: Exploring type hierarchies
Write-Host "`nStep 6: Exploring JsonSerializer hierarchy..." -ForegroundColor Yellow
Write-Host "Command: apilens hierarchy 'JsonSerializer'" -ForegroundColor Gray
& $apilens hierarchy "JsonSerializer" | Select-Object -First 20
Start-Sleep -Seconds 2

# Step 7: Finding entry point methods
Write-Host "`nStep 7: Finding Create methods (entry points)..." -ForegroundColor Yellow
Write-Host "Command: apilens query 'Create' --type method --max 5" -ForegroundColor Gray
& $apilens query "Create" --type method --max 5
Start-Sleep -Seconds 2

# Step 8: Browsing namespaces
Write-Host "`nStep 8: Listing types in a specific namespace..." -ForegroundColor Yellow
Write-Host "Command: apilens list-types --namespace 'Newtonsoft.Json.Linq' --max 10" -ForegroundColor Gray
& $apilens list-types --namespace "Newtonsoft.Json.Linq" --max 10
Start-Sleep -Seconds 2

# Summary
Write-Host "`n=== Demo Complete ===" -ForegroundColor Green
Write-Host @"
The enhanced drill-down experience provides:
1. Package exploration with 'explore' command - overview and entry points
2. Smart grouping with '--group-by' - organize results by category, namespace, etc.
3. Quality-first sorting with '--quality-first' - prioritize well-documented APIs
4. Entry point discovery with '--entry-points' - find main methods to start with
5. Interactive navigation - guided next steps after each command

This workflow transforms ApiLens from a search tool into an intelligent API explorer!
"@ -ForegroundColor Cyan