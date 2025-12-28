#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Demonstrates the enhanced wildcard search with member type and namespace/assembly filters.

.DESCRIPTION
    Shows how to use the new SearchWithFilters functionality that allows combining
    wildcard name searches with member type filtering and namespace/assembly patterns.

.EXAMPLE
    ./enhanced-wildcard-demo.ps1
#>

$ErrorActionPreference = "Stop"

Write-Host "`n🚀 ApiLens Enhanced Wildcard Search Demo" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host "New feature: Combined wildcard search with type and namespace filtering" -ForegroundColor Gray

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
$tmpBase = Join-Path ([System.IO.Path]::GetTempPath()) "apilens-demo"
$indexPath = Join-Path $tmpBase "indexes/enhanced-wildcard-index"
if (Test-Path $indexPath) { Remove-Item $indexPath -Recurse -Force }

Write-Host "`n📦 Setting up demo with popular NuGet packages..." -ForegroundColor Yellow

# Index some common packages
Write-Host "Indexing packages from NuGet cache..." -ForegroundColor Gray
& "$apilens" nuget --filter "newtonsoft.*" --latest-only --index "$indexPath" | Out-Null
& "$apilens" nuget --filter "microsoft.extensions.*" --latest-only --index "$indexPath" --max-packages 3 | Out-Null

Write-Host "✅ Packages indexed successfully!" -ForegroundColor Green

# PART 1: Basic Enhanced Wildcard Search
Write-Host "`n`n═══════════════════════════════════════════════════════════════" -ForegroundColor DarkCyan
Write-Host "  PART 1: ENHANCED WILDCARD WITH MEMBER TYPE FILTERING" -ForegroundColor Yellow
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor DarkCyan

Write-Host "`n1️⃣  Find all methods with 'Parse' in the name:" -ForegroundColor Magenta
Write-Host "Command: " -NoNewline -ForegroundColor DarkGray
Write-Host "apilens query 'Parse' --member-type Method --max 10" -ForegroundColor Yellow
& "$apilens" query "Parse" --member-type Method --index "$indexPath" --max 10

Write-Host "`n2️⃣  Find all Exception classes (Type members only):" -ForegroundColor Magenta
Write-Host "Command: " -NoNewline -ForegroundColor DarkGray
Write-Host "apilens query 'Exception' --member-type Type --max 10" -ForegroundColor Yellow
& "$apilens" query "Exception" --member-type Type --index "$indexPath" --max 10

Write-Host "`n3️⃣  Find all properties with 'Count' in the name:" -ForegroundColor Magenta
Write-Host "Command: " -NoNewline -ForegroundColor DarkGray
Write-Host "apilens query 'Count' --member-type Property --max 10" -ForegroundColor Yellow
& "$apilens" query "Count" --member-type Property --index "$indexPath" --max 10

# PART 2: Namespace Pattern Filtering
Write-Host "`n`n═══════════════════════════════════════════════════════════════" -ForegroundColor DarkCyan
Write-Host "  PART 2: WILDCARD WITH NAMESPACE FILTERING" -ForegroundColor Yellow
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor DarkCyan

Write-Host "`n1️⃣  Find 'Convert' methods in Newtonsoft.Json namespace:" -ForegroundColor Magenta
Write-Host "Command: " -NoNewline -ForegroundColor DarkGray
Write-Host "apilens query 'Convert' --namespace 'Newtonsoft.Json' --max 10" -ForegroundColor Yellow
& "$apilens" query "Convert" --namespace "Newtonsoft.Json" --index "$indexPath" --max 10

Write-Host "`n2️⃣  Find methods in any Linq-related namespace:" -ForegroundColor Magenta
Write-Host "Command: " -NoNewline -ForegroundColor DarkGray
Write-Host "apilens query 'Select' --namespace '*Linq*' --max 10" -ForegroundColor Yellow
& "$apilens" query "Select" --namespace "*Linq*" --index "$indexPath" --max 10

Write-Host "`n3️⃣  Find types in sub-namespaces using wildcards:" -ForegroundColor Magenta
Write-Host "Command: " -NoNewline -ForegroundColor DarkGray
Write-Host "apilens query 'Token' --namespace 'Newtonsoft.Json.*' --member-type Type --max 10" -ForegroundColor Yellow
& "$apilens" query "Token" --namespace "Newtonsoft.Json.*" --member-type Type --index "$indexPath" --max 10

# PART 3: Assembly Pattern Filtering
Write-Host "`n`n═══════════════════════════════════════════════════════════════" -ForegroundColor DarkCyan
Write-Host "  PART 3: WILDCARD WITH ASSEMBLY FILTERING" -ForegroundColor Yellow
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor DarkCyan

Write-Host "`n1️⃣  Find methods in Newtonsoft.Json assembly:" -ForegroundColor Magenta
Write-Host "Command: " -NoNewline -ForegroundColor DarkGray
Write-Host "apilens query 'Serialize' --assembly 'Newtonsoft.Json' --member-type Method --max 10" -ForegroundColor Yellow
& "$apilens" query "Serialize" --assembly "Newtonsoft.Json" --member-type Method --index "$indexPath" --max 10

Write-Host "`n2️⃣  Find types in Microsoft assemblies using wildcards:" -ForegroundColor Magenta
Write-Host "Command: " -NoNewline -ForegroundColor DarkGray
Write-Host "apilens query 'Options' --assembly 'Microsoft.*' --member-type Type --max 10" -ForegroundColor Yellow
& "$apilens" query "Options" --assembly "Microsoft.*" --member-type Type --index "$indexPath" --max 10

# PART 4: Combined Filters
Write-Host "`n`n═══════════════════════════════════════════════════════════════" -ForegroundColor DarkCyan
Write-Host "  PART 4: COMBINING MULTIPLE FILTERS" -ForegroundColor Yellow
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor DarkCyan

Write-Host "`n1️⃣  Methods in specific namespace and assembly:" -ForegroundColor Magenta
Write-Host "Command: " -NoNewline -ForegroundColor DarkGray
Write-Host "apilens query 'Write' --member-type Method --namespace 'Newtonsoft.Json' --assembly 'Newtonsoft.Json' --max 10" -ForegroundColor Yellow
& "$apilens" query "Write" --member-type Method --namespace "Newtonsoft.Json" --assembly "Newtonsoft.Json" --index "$indexPath" --max 10

Write-Host "`n2️⃣  Properties with wildcards in namespace and assembly:" -ForegroundColor Magenta
Write-Host "Command: " -NoNewline -ForegroundColor DarkGray
Write-Host "apilens query 'Value' --member-type Property --namespace 'Newtonsoft.*' --assembly 'Newtonsoft.*' --max 10" -ForegroundColor Yellow
& "$apilens" query "Value" --member-type Property --namespace "Newtonsoft.*" --assembly "Newtonsoft.*" --index "$indexPath" --max 10

Write-Host "`n3️⃣  Find all Exception types in System namespaces:" -ForegroundColor Magenta
Write-Host "Command: " -NoNewline -ForegroundColor DarkGray
Write-Host "apilens query 'Exception' --member-type Type --namespace 'System.*' --max 10" -ForegroundColor Yellow
& "$apilens" query "Exception" --member-type Type --namespace "System.*" --index "$indexPath" --max 10

# PART 5: JSON Output for Automation
Write-Host "`n`n═══════════════════════════════════════════════════════════════" -ForegroundColor DarkCyan
Write-Host "  PART 5: JSON OUTPUT WITH FILTERS" -ForegroundColor Yellow
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor DarkCyan

Write-Host "`n📊 Get JSON output with metadata:" -ForegroundColor Magenta
Write-Host "Command: " -NoNewline -ForegroundColor DarkGray
Write-Host "apilens query 'Json' --member-type Type --namespace 'Newtonsoft.*' --format json --max 5" -ForegroundColor Yellow
$jsonOutput = & "$apilens" query "Json" --member-type Type --namespace "Newtonsoft.*" --format json --index "$indexPath" --max 5
if ($jsonOutput) {
    # Pretty print a portion of the JSON
    $obj = $jsonOutput | ConvertFrom-Json
    Write-Host "`nResults Count: $($obj.results.Count)" -ForegroundColor Green
    Write-Host "Search Time: $($obj.metadata.searchTime)" -ForegroundColor Green
    Write-Host "Total Count: $($obj.metadata.totalCount)" -ForegroundColor Green
    if ($obj.results.Count -gt 0) {
        Write-Host "`nFirst result:" -ForegroundColor Cyan
        Write-Host "  Name: $($obj.results[0].name)" -ForegroundColor Gray
        Write-Host "  Type: $($obj.results[0].memberType)" -ForegroundColor Gray
        Write-Host "  Namespace: $($obj.results[0].namespace)" -ForegroundColor Gray
        Write-Host "  Assembly: $($obj.results[0].assembly)" -ForegroundColor Gray
    }
}

# PART 6: Practical Use Cases
Write-Host "`n`n═══════════════════════════════════════════════════════════════" -ForegroundColor DarkCyan
Write-Host "  PART 6: PRACTICAL USE CASES" -ForegroundColor Yellow
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor DarkCyan

Write-Host "`n📋 Use Case: Find all serialization-related methods" -ForegroundColor Green
& "$apilens" query "Serialize" --member-type Method --namespace "Newtonsoft.*" --index "$indexPath" --max 5

Write-Host "`n📋 Use Case: Find all collection types" -ForegroundColor Green
& "$apilens" query "Collection" --member-type Type --index "$indexPath" --max 5

Write-Host "`n📋 Use Case: Find configuration-related properties" -ForegroundColor Green
& "$apilens" query "Config" --member-type Property --namespace "Microsoft.*" --index "$indexPath" --max 5

# Summary
Write-Host "`n`n✨ Summary of Enhanced Wildcard Features" -ForegroundColor Cyan
Write-Host @"

🎯 New Capabilities:
   • Automatic wildcard wrapping: 'Parse' becomes '*Parse*' for convenience
   • Filter by member type: --member-type [Type|Method|Property|Field|Event]
   • Filter by namespace pattern: --namespace 'pattern' (supports wildcards)
   • Filter by assembly pattern: --assembly 'pattern' (supports wildcards)
   • Combine all filters for precise searches

🔍 Search Examples:
   • Find all Parse methods: query 'Parse' --member-type Method
   • Find Exception types in System: query 'Exception' --member-type Type --namespace 'System.*'
   • Find serialization in Newtonsoft: query 'Serialize' --assembly 'Newtonsoft.*'

💡 Tips:
   • Name patterns automatically get wildcards added if not present
   • Use explicit wildcards for more control: 'Parse*' vs '*Parse' vs '*Parse*'
   • Combine filters to narrow results effectively
   • JSON output includes metadata for automation

This enhanced search makes ApiLens perfect for:
   - Finding specific API members across large codebases
   - API discovery and exploration
   - Security audits (find all Exception types, security methods)
   - Documentation generation with precise filtering
   - Integration with development tools and LLMs
"@ -ForegroundColor Gray

# Cleanup
Write-Host "`n🧹 Cleaning up demo index..." -ForegroundColor Yellow
Remove-Item $indexPath -Recurse -Force
Write-Host "✅ Demo complete!" -ForegroundColor Green