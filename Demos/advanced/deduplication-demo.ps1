#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Demonstrates the improved deduplication features in ApiLens.

.DESCRIPTION
    Shows how the --distinct flag now properly works to eliminate duplicate
    entries across different framework versions, and how property type linking
    provides better type information for properties.

.EXAMPLE
    ./deduplication-demo.ps1
#>

$ErrorActionPreference = "Stop"

Write-Host "`n🔄 ApiLens Deduplication & Property Type Improvements Demo" -ForegroundColor Cyan
Write-Host "=============================================================" -ForegroundColor Cyan
Write-Host "New features: Proper deduplication and property type linking" -ForegroundColor Gray

# Build if needed
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$apilens = Join-Path $repoRoot "Solutions/ApiLens.Cli/bin/Debug/net10.0/apilens"
if ($IsWindows -or $env:OS -eq "Windows_NT") { 
    $apilens += ".exe" 
}
if (-not (Test-Path $apilens)) {
    Write-Host "`nBuilding ApiLens..." -ForegroundColor Yellow
    $csproj = Join-Path $repoRoot "Solutions/ApiLens.Cli/ApiLens.Cli.csproj"
    dotnet build $csproj --verbosity quiet
}

# Set up demo index
$tmpBase = Join-Path ([System.IO.Path]::GetTempPath()) "apilens-dedup-demo"
$indexPath = Join-Path $tmpBase "indexes/deduplication-index"
if (Test-Path $indexPath) { Remove-Item $indexPath -Recurse -Force }

Write-Host "`n📦 Setting up demo with multi-framework packages..." -ForegroundColor Yellow

# Index packages that have multiple framework versions to demonstrate deduplication
Write-Host "Indexing packages with multiple framework targets..." -ForegroundColor Gray
& "$apilens" nuget --filter "newtonsoft.json" --max-packages 1 --index "$indexPath" | Out-Null
& "$apilens" nuget --filter "system.text.json" --max-packages 1 --index "$indexPath" | Out-Null

Write-Host "✅ Multi-framework packages indexed successfully!" -ForegroundColor Green

# PART 1: Deduplication Problem (Before Fix)
Write-Host "`n`n═══════════════════════════════════════════════════════════════" -ForegroundColor DarkCyan
Write-Host "  PART 1: DEDUPLICATION - THE PROBLEM AND SOLUTION" -ForegroundColor Yellow
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor DarkCyan

Write-Host "`n❌ BEFORE: Without deduplication (--distinct false)" -ForegroundColor Red
Write-Host "Problem: Types appear multiple times (once per framework version)" -ForegroundColor Gray
Write-Host "`nCommand: " -NoNewline -ForegroundColor DarkGray
Write-Host "apilens list-types --package 'Newtonsoft.Json' --distinct false --max 10" -ForegroundColor Yellow
& "$apilens" list-types --package "Newtonsoft.Json" --distinct false --index "$indexPath" --max 10

Write-Host "`n✅ AFTER: With deduplication (--distinct true - now the default!)" -ForegroundColor Green
Write-Host "Solution: Each type appears once, best framework version is selected" -ForegroundColor Gray
Write-Host "`nCommand: " -NoNewline -ForegroundColor DarkGray
Write-Host "apilens list-types --package 'Newtonsoft.Json' --distinct true --max 10" -ForegroundColor Yellow
& "$apilens" list-types --package "Newtonsoft.Json" --distinct true --index "$indexPath" --max 10

Write-Host "`n💡 Key Improvement: --distinct now defaults to 'true' for better user experience!" -ForegroundColor Cyan

# PART 2: Deduplication with Members
Write-Host "`n`n═══════════════════════════════════════════════════════════════" -ForegroundColor DarkCyan
Write-Host "  PART 2: DEDUPLICATION WITH TYPE MEMBERS" -ForegroundColor Yellow
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor DarkCyan

Write-Host "`n1️⃣  Without deduplication - shows duplicate members:" -ForegroundColor Magenta
Write-Host "Command: " -NoNewline -ForegroundColor DarkGray
Write-Host "apilens members 'JsonConvert' --distinct false --max 10" -ForegroundColor Yellow
& "$apilens" members "JsonConvert" --distinct false --index "$indexPath" --max 10

Write-Host "`n2️⃣  With deduplication - clean, unique results:" -ForegroundColor Magenta
Write-Host "Command: " -NoNewline -ForegroundColor DarkGray
Write-Host "apilens members 'JsonConvert' --distinct true --max 10" -ForegroundColor Yellow
& "$apilens" members "JsonConvert" --distinct true --index "$indexPath" --max 10

# PART 3: Framework Version Prioritization
Write-Host "`n`n═══════════════════════════════════════════════════════════════" -ForegroundColor DarkCyan
Write-Host "  PART 3: SMART FRAMEWORK VERSION PRIORITIZATION" -ForegroundColor Yellow
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor DarkCyan

Write-Host "`n🎯 The deduplication service uses intelligent framework prioritization:" -ForegroundColor Cyan
Write-Host @"

Priority Order (highest to lowest):
1. net10.0, net8.0, net7.0, net6.0 (latest .NET versions first)
2. net5.0, netcoreapp3.1, netcoreapp3.0 (older .NET Core)
3. netstandard2.1, netstandard2.0 (cross-platform)
4. net48, net472, etc. (legacy .NET Framework)

This ensures you see the most modern, feature-complete version of each API!
"@ -ForegroundColor Gray

Write-Host "`n📊 JSON output shows framework selection details:" -ForegroundColor Magenta
Write-Host "Command: " -NoNewline -ForegroundColor DarkGray
Write-Host "apilens list-types --package 'Newtonsoft.Json' --format json --max 3" -ForegroundColor Yellow
$jsonOutput = & "$apilens" list-types --package "Newtonsoft.Json" --format json --index "$indexPath" --max 3
if ($jsonOutput) {
    $obj = $jsonOutput | ConvertFrom-Json
    Write-Host "`nDeduplication Statistics:" -ForegroundColor Green
    Write-Host "  Unique Results: $($obj.results.Count)" -ForegroundColor Gray
    Write-Host "  Search Time: $($obj.metadata.searchTime)" -ForegroundColor Gray
    if ($obj.results.Count -gt 0) {
        Write-Host "`nFramework versions selected:" -ForegroundColor Cyan
        $obj.results | ForEach-Object {
            Write-Host "  $($_.name): $($_.targetFramework)" -ForegroundColor Gray
        }
    }
}

# PART 4: Property Type Linking Feature
Write-Host "`n`n═══════════════════════════════════════════════════════════════" -ForegroundColor DarkCyan
Write-Host "  PART 4: PROPERTY TYPE LINKING IMPROVEMENTS" -ForegroundColor Yellow
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor DarkCyan

Write-Host "`n💡 New Feature: Properties now show type information!" -ForegroundColor Cyan
Write-Host "The system links properties to their getter methods to extract type information." -ForegroundColor Gray

Write-Host "`n🔍 Find properties and see their types:" -ForegroundColor Magenta
Write-Host "Command: " -NoNewline -ForegroundColor DarkGray
Write-Host "apilens query 'Value' --member-type Property --max 5" -ForegroundColor Yellow
& "$apilens" query "Value" --member-type Property --index "$indexPath" --max 5

Write-Host "`n📋 List type members showing property types:" -ForegroundColor Magenta
Write-Host "Command: " -NoNewline -ForegroundColor DarkGray
Write-Host "apilens members 'JsonSerializer' --max 10" -ForegroundColor Yellow
& "$apilens" members "JsonSerializer" --index "$indexPath" --max 10

Write-Host "`n🔬 Technical Detail: Property Type Extraction" -ForegroundColor Cyan
Write-Host @"

How it works:
1. XML documentation typically lacks property type information
2. ApiLens now links properties to their getter methods
3. Property 'SomeProperty' → Method 'get_SomeProperty'  
4. The getter method's return type becomes the property's type
5. This provides richer metadata for API exploration

Example: Property 'Count' → finds 'get_Count() : int' → Property type = 'int'
"@ -ForegroundColor Gray

# PART 5: Performance and Statistics
Write-Host "`n`n═══════════════════════════════════════════════════════════════" -ForegroundColor DarkCyan
Write-Host "  PART 5: DEDUPLICATION IMPACT ON PERFORMANCE" -ForegroundColor Yellow
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor DarkCyan

Write-Host "`n📈 Compare search performance with and without deduplication:" -ForegroundColor Magenta

# Measure search without deduplication
Write-Host "`n⏱️  Search without deduplication:" -ForegroundColor Yellow
$start = Get-Date
$resultWithoutDedup = & "$apilens" query "Json" --distinct false --format json --index "$indexPath" --max 50
$timeWithoutDedup = (Get-Date) - $start
if ($resultWithoutDedup) {
    $objWithoutDedup = $resultWithoutDedup | ConvertFrom-Json
    Write-Host "  Results: $($objWithoutDedup.results.Count)" -ForegroundColor Gray
    Write-Host "  Time: $($timeWithoutDedup.TotalMilliseconds) ms" -ForegroundColor Gray
}

# Measure search with deduplication  
Write-Host "`n⏱️  Search with deduplication:" -ForegroundColor Green
$start = Get-Date
$resultWithDedup = & "$apilens" query "Json" --distinct true --format json --index "$indexPath" --max 50
$timeWithDedup = (Get-Date) - $start
if ($resultWithDedup) {
    $objWithDedup = $resultWithDedup | ConvertFrom-Json
    Write-Host "  Results: $($objWithDedup.results.Count)" -ForegroundColor Gray
    Write-Host "  Time: $($timeWithDedup.TotalMilliseconds) ms" -ForegroundColor Gray
    
    if ($objWithoutDedup -and $objWithDedup -and $objWithoutDedup.results.Count -gt 0) {
        $reduction = [math]::Round((1 - $objWithDedup.results.Count / $objWithoutDedup.results.Count) * 100, 1)
        Write-Host "  🎯 Result reduction: $reduction%" -ForegroundColor Green
    }
}

# PART 6: Practical Use Cases
Write-Host "`n`n═══════════════════════════════════════════════════════════════" -ForegroundColor DarkCyan
Write-Host "  PART 6: PRACTICAL USE CASES FOR DEDUPLICATION" -ForegroundColor Yellow
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor DarkCyan

Write-Host "`n📋 Use Case 1: API Documentation Generation" -ForegroundColor Green
Write-Host "Generate clean documentation without duplicate entries:" -ForegroundColor Gray
& "$apilens" list-types --assembly "Newtonsoft.Json" --distinct true --index "$indexPath" --max 5

Write-Host "`n📋 Use Case 2: LLM Integration with Clean Data" -ForegroundColor Green
Write-Host "Provide LLMs with deduplicated, accurate API information:" -ForegroundColor Gray
$cleanJson = & "$apilens" query "Parse" --member-type Method --format json --distinct true --index "$indexPath" --max 3
if ($cleanJson) {
    $obj = $cleanJson | ConvertFrom-Json
    Write-Host "✅ Clean JSON with $($obj.results.Count) unique parse methods" -ForegroundColor Cyan
}

Write-Host "`n📋 Use Case 3: API Discovery and Exploration" -ForegroundColor Green
Write-Host "Find unique APIs without clutter from multiple framework versions:" -ForegroundColor Gray
& "$apilens" query "Serialize" --member-type Method --namespace "Newtonsoft.*" --distinct true --index "$indexPath" --max 5

# Summary
Write-Host "`n`n✨ Summary of Deduplication Improvements" -ForegroundColor Cyan
Write-Host @"

🎯 Key Improvements:
   ✅ Fixed --distinct flag - it now works properly!
   ✅ --distinct defaults to 'true' for better user experience
   ✅ Intelligent framework version prioritization (net10.0 > net8.0 > netstandard2.0)
   ✅ Property type linking for richer metadata
   ✅ Consistent deduplication across all commands (list-types, members, query)

🔍 Deduplication Features:
   • Eliminates duplicate entries across framework versions
   • Selects the most appropriate framework version automatically
   • Maintains API completeness while reducing noise
   • Works with all output formats (table, JSON, markdown)

🏷️  Property Type Linking:
   • Properties now show type information when available
   • Links properties to their getter methods automatically
   • Provides richer metadata for API exploration
   • Improves LLM integration with better type information

💡 Benefits:
   • Cleaner, more focused search results
   • Better performance with fewer duplicates to process
   • Improved API documentation generation
   • Enhanced LLM integration with accurate, unique data
   • Better developer experience with less noise

Perfect for:
   - API documentation tools
   - LLM integration and MCP servers
   - Development environment plugins
   - Code analysis and discovery tools
"@ -ForegroundColor Gray

# Cleanup
Write-Host "`n🧹 Cleaning up demo index..." -ForegroundColor Yellow
Remove-Item $indexPath -Recurse -Force
Write-Host "✅ Deduplication demo complete!" -ForegroundColor Green