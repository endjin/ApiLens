#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Demonstrates the new leading wildcard search and type listing capabilities.

.DESCRIPTION
    Shows how to use leading wildcards in exception searches and list types
    from specific packages, assemblies, and namespaces with wildcard support.

.EXAMPLE
    ./wildcard-and-types-demo.ps1
#>

$ErrorActionPreference = "Stop"

Write-Host "`n🚀 ApiLens Leading Wildcards & Type Listing Demo" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host "New features: Leading wildcard support and type exploration" -ForegroundColor Gray

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
$indexPath = Join-Path $tmpBase "indexes/wildcard-demo-index"
if (Test-Path $indexPath) { Remove-Item $indexPath -Recurse -Force }

Write-Host "`n📦 Setting up demo with popular NuGet packages..." -ForegroundColor Yellow

# Index some common packages that are likely to be in cache
Write-Host "Indexing packages from NuGet cache..." -ForegroundColor Gray
& "$apilens" nuget --filter "newtonsoft.*" --latest-only --index "$indexPath" | Out-Null
& "$apilens" nuget --filter "microsoft.extensions.*" --latest-only --index "$indexPath" --max-packages 5 | Out-Null
& "$apilens" nuget --filter "system.*" --latest-only --index "$indexPath" --max-packages 5 | Out-Null

Write-Host "✅ Packages indexed successfully!" -ForegroundColor Green

# PART 1: Leading Wildcard Demonstrations
Write-Host "`n`n═══════════════════════════════════════════════════════════════" -ForegroundColor DarkCyan
Write-Host "  PART 1: LEADING WILDCARD SEARCH CAPABILITIES" -ForegroundColor Yellow
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor DarkCyan

Write-Host "`n📌 Before: Without leading wildcards (would fail in older versions)" -ForegroundColor Red
Write-Host "Example query that would have failed: exceptions '*IOException'" -ForegroundColor DarkGray
Write-Host "Error: Leading wildcards not supported in standard Lucene queries" -ForegroundColor DarkRed

Write-Host "`n✨ Now: Leading wildcards fully supported!" -ForegroundColor Green

Write-Host "`n1️⃣  Find all exceptions ending with 'Exception':" -ForegroundColor Magenta
Write-Host "Command: " -NoNewline -ForegroundColor DarkGray
Write-Host "apilens exceptions '*Exception' --max 10" -ForegroundColor Yellow
& "$apilens" exceptions "*Exception" --index "$indexPath" --max 10

Write-Host "`n2️⃣  Find all IO-related exceptions:" -ForegroundColor Magenta
Write-Host "Command: " -NoNewline -ForegroundColor DarkGray
Write-Host "apilens exceptions '*IOException'" -ForegroundColor Yellow
& "$apilens" exceptions "*IOException" --index "$indexPath"

Write-Host "`n3️⃣  Find serialization-related exceptions:" -ForegroundColor Magenta
Write-Host "Command: " -NoNewline -ForegroundColor DarkGray
Write-Host "apilens exceptions '*Serialization*'" -ForegroundColor Yellow
& "$apilens" exceptions "*Serialization*" --index "$indexPath"

Write-Host "`n4️⃣  Complex wildcard patterns:" -ForegroundColor Magenta
Write-Host "Command: " -NoNewline -ForegroundColor DarkGray
Write-Host "apilens exceptions '*Argument*Exception'" -ForegroundColor Yellow
& "$apilens" exceptions "*Argument*Exception" --index "$indexPath"

Write-Host "`n5️⃣  Using ? for single character wildcard:" -ForegroundColor Magenta
Write-Host "Command: " -NoNewline -ForegroundColor DarkGray
Write-Host "apilens exceptions 'System.?rrayTypeMismatchException'" -ForegroundColor Yellow
& "$apilens" exceptions "System.?rrayTypeMismatchException" --index "$indexPath"

# PART 2: Type Listing Features
Write-Host "`n`n═══════════════════════════════════════════════════════════════" -ForegroundColor DarkCyan
Write-Host "  PART 2: TYPE LISTING & EXPLORATION" -ForegroundColor Yellow
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor DarkCyan

Write-Host "`n🎯 List types from specific packages and namespaces" -ForegroundColor Cyan

Write-Host "`n1️⃣  List types from a specific package:" -ForegroundColor Magenta
Write-Host "Command: " -NoNewline -ForegroundColor DarkGray
Write-Host "apilens list-types --package 'newtonsoft.json' --max 15" -ForegroundColor Yellow
& "$apilens" list-types --package "newtonsoft.json" --index "$indexPath" --max 15

Write-Host "`n2️⃣  List types from a specific namespace:" -ForegroundColor Magenta
Write-Host "Command: " -NoNewline -ForegroundColor DarkGray
Write-Host "apilens list-types --namespace 'Newtonsoft.Json.Linq' --max 10" -ForegroundColor Yellow
& "$apilens" list-types --namespace "Newtonsoft.Json.Linq" --index "$indexPath" --max 10

Write-Host "`n3️⃣  Using wildcards in namespace patterns:" -ForegroundColor Magenta
Write-Host "Command: " -NoNewline -ForegroundColor DarkGray
Write-Host "apilens list-types --namespace 'Newtonsoft.Json.Convert*' --max 10" -ForegroundColor Yellow
& "$apilens" list-types --namespace "Newtonsoft.Json.Convert*" --index "$indexPath" --max 10

Write-Host "`n4️⃣  List types from Microsoft.Extensions packages:" -ForegroundColor Magenta
Write-Host "Command: " -NoNewline -ForegroundColor DarkGray
Write-Host "apilens list-types --package 'microsoft.extensions.*' --max 10" -ForegroundColor Yellow
& "$apilens" list-types --package "microsoft.extensions.*" --index "$indexPath" --max 10

Write-Host "`n5️⃣  Combine filters - package and namespace:" -ForegroundColor Magenta
Write-Host "Command: " -NoNewline -ForegroundColor DarkGray
Write-Host "apilens list-types --package 'newtonsoft.json' --namespace 'Newtonsoft.Json.Schema' --max 10" -ForegroundColor Yellow
& "$apilens" list-types --package "newtonsoft.json" --namespace "Newtonsoft.Json.Schema" --index "$indexPath" --max 10

Write-Host "`n6️⃣  Include all members (not just types):" -ForegroundColor Magenta
Write-Host "Command: " -NoNewline -ForegroundColor DarkGray
Write-Host "apilens list-types --namespace 'Newtonsoft.Json' --include-members --max 20" -ForegroundColor Yellow
& "$apilens" list-types --namespace "Newtonsoft.Json" --index "$indexPath" --include-members --max 20

Write-Host "`n7️⃣  JSON output for programmatic processing:" -ForegroundColor Magenta
Write-Host "Command: " -NoNewline -ForegroundColor DarkGray
Write-Host "apilens list-types --package 'newtonsoft.json' --format json --max 5" -ForegroundColor Yellow
$jsonOutput = & "$apilens" list-types --package "newtonsoft.json" --index "$indexPath" --format json --max 5
if ($jsonOutput -and $jsonOutput -ne "[]") {
    Write-Host $jsonOutput -ForegroundColor Cyan
    
    # Parse and show summary
    try {
        $types = $jsonOutput | ConvertFrom-Json
        Write-Host "`n📊 JSON Processing Example:" -ForegroundColor Green
        Write-Host "  Total types returned: $($types.Count)" -ForegroundColor Gray
        if ($types.Count -gt 0) {
            Write-Host "  First type: $($types[0].fullName)" -ForegroundColor Gray
            Write-Host "  Namespace: $($types[0].namespace)" -ForegroundColor Gray
            Write-Host "  Package: $($types[0].packageId)" -ForegroundColor Gray
        }
    } catch {
        Write-Host "JSON parsing example skipped" -ForegroundColor Gray
    }
}

Write-Host "`n8️⃣  Markdown output for documentation:" -ForegroundColor Magenta
Write-Host "Command: " -NoNewline -ForegroundColor DarkGray
Write-Host "apilens list-types --namespace 'Newtonsoft.Json.Converters' --format markdown --max 5" -ForegroundColor Yellow
& "$apilens" list-types --namespace "Newtonsoft.Json.Converters" --index "$indexPath" --format markdown --max 5

# PART 3: Advanced Use Cases
Write-Host "`n`n═══════════════════════════════════════════════════════════════" -ForegroundColor DarkCyan
Write-Host "  PART 3: ADVANCED USE CASES" -ForegroundColor Yellow
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor DarkCyan

Write-Host "`n🔗 Combining Features - Workflow Examples" -ForegroundColor Cyan

Write-Host "`n📋 Use Case 1: Exploring a Package API Surface" -ForegroundColor Green
Write-Host "Step 1 - List all types in the package:" -ForegroundColor Gray
& "$apilens" list-types --package "newtonsoft.json" --max 5 --index "$indexPath"

Write-Host "`nStep 2 - Find methods that might throw exceptions:" -ForegroundColor Gray
& "$apilens" exceptions "*Exception" --index "$indexPath" --max 3

Write-Host "`nStep 3 - Look for specific patterns in the API:" -ForegroundColor Gray
& "$apilens" query "Serialize*" --index "$indexPath" --max 3

Write-Host "`n📋 Use Case 2: Security Analysis" -ForegroundColor Green
Write-Host "Find all security-related exceptions:" -ForegroundColor Gray
& "$apilens" exceptions "*Security*" --index "$indexPath"
& "$apilens" exceptions "*Unauthorized*" --index "$indexPath"
& "$apilens" exceptions "*Permission*" --index "$indexPath"

Write-Host "`n📋 Use Case 3: Package Discovery" -ForegroundColor Green
Write-Host "Discover what packages and types are available:" -ForegroundColor Gray

# List packages
Write-Host "`nPackages in index:" -ForegroundColor Yellow
& "$apilens" nuget --list --filter "*"

# Pick a package and explore it
Write-Host "`nExplore types in Microsoft.Extensions packages:" -ForegroundColor Yellow
& "$apilens" list-types --package "microsoft.extensions.*" --group-by package --max 15 --index "$indexPath"

# PART 4: Performance Considerations
Write-Host "`n`n═══════════════════════════════════════════════════════════════" -ForegroundColor DarkCyan
Write-Host "  PART 4: PERFORMANCE NOTES" -ForegroundColor Yellow
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor DarkCyan

Write-Host "`n⚡ Performance Considerations for Leading Wildcards:" -ForegroundColor Cyan
Write-Host @"

1. Leading wildcards (*Exception, *IOException) are more expensive than:
   - Exact matches: "System.IO.IOException"
   - Prefix searches: "System.IO.*"
   
2. Performance tips:
   - Use --max parameter to limit results
   - Be as specific as possible with patterns
   - Consider using namespace filters to narrow scope
   
3. The implementation uses Lucene's WildcardQuery with AllowLeadingWildcard=true
   - This enables powerful pattern matching
   - But may impact performance on very large indexes
   
4. Best practices:
   - Start with more specific patterns when possible
   - Use leading wildcards when you need to find all variants
   - Combine with other filters (package, namespace) to reduce search space
"@ -ForegroundColor Gray

# Summary
Write-Host "`n`n✨ Summary of New Features" -ForegroundColor Cyan
Write-Host @"

🎯 Leading Wildcard Support:
   • Search for exceptions ending with specific patterns (*Exception)
   • Find all variants of a type (*IOException finds IOException, FileNotFoundException, etc.)
   • Complex patterns with multiple wildcards (*Argument*Exception)
   • Single character wildcards with ? 

📦 Type Listing Command (list-types):
   • Filter by package (--package)
   • Filter by assembly (--assembly)
   • Filter by namespace (--namespace)
   • All filters support wildcards
   • Multiple output formats (table, json, markdown)
   • Group results by package, assembly, or namespace
   • Include all members or just types

🔄 Integration Benefits:
   • Better API exploration and discovery
   • Enhanced pattern matching for code analysis
   • Structured output for automation and LLM integration
   • Comprehensive type and package exploration

These features make ApiLens more powerful for:
   - API documentation generation
   - Code analysis and exploration
   - Security audits (finding all security exceptions)
   - Package API surface discovery
   - Automated tooling and LLM integration
"@ -ForegroundColor Gray

# Cleanup
Write-Host "`n🧹 Cleaning up demo index..." -ForegroundColor Yellow
Remove-Item $indexPath -Recurse -Force
Write-Host "✅ Demo complete!" -ForegroundColor Green