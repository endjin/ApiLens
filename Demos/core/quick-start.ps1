#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Quick demonstration of ApiLens core features.

.DESCRIPTION
    A simplified demo script that shows the essential ApiLens features
    in under 5 minutes.
#>

Write-Host "`n🔍 ApiLens Quick Demo" -ForegroundColor Cyan
Write-Host "===================" -ForegroundColor Cyan

# Helper function to display commands
function Write-Command {
    param(
        [string]$Command,
        [string]$Description
    )
    Write-Host "`n$Description" -ForegroundColor Gray
    Write-Host "Command: " -NoNewline -ForegroundColor DarkGray
    Write-Host "$Command" -ForegroundColor Yellow
}

# Build if needed
if (-not (Test-Path "./Solutions/ApiLens.Cli/bin/Debug/net9.0/apilens*")) {
    Write-Host "`n📦 Building ApiLens..." -ForegroundColor Yellow
    dotnet build ./Solutions/ApiLens.Cli/ApiLens.Cli.csproj --verbosity quiet
}

# Create demo data
Write-Host "`n📄 Creating sample XML documentation..." -ForegroundColor Yellow
$tmpBase = Join-Path ([System.IO.Path]::GetTempPath()) "apilens-demo"
$demoDir = Join-Path $tmpBase "docs/quick-demo"
$indexDir = Join-Path $tmpBase "indexes/quick-demo-index"
New-Item -ItemType Directory -Path $demoDir -Force | Out-Null

@'
<?xml version="1.0"?>
<doc>
    <assembly><name>DemoLib</name></assembly>
    <members>
        <member name="T:DemoLib.StringHelper">
            <summary>Provides utility methods for string manipulation and text processing.</summary>
            <seealso cref="T:System.String"/>
        </member>
        <member name="M:DemoLib.StringHelper.Reverse(System.String)">
            <summary>Reverses a string efficiently using StringBuilder.</summary>
            <param name="input">The string to reverse.</param>
            <returns>The reversed string.</returns>
        </member>
        <member name="M:DemoLib.StringHelper.CountWords(System.String)">
            <summary>Counts words in a string using advanced tokenization.</summary>
            <param name="text">The text to analyze.</param>
            <returns>The word count.</returns>
        </member>
        <member name="T:DemoLib.MathHelper">
            <summary>Provides mathematical utility methods for calculations.</summary>
        </member>
        <member name="M:DemoLib.MathHelper.IsPrime(System.Int32)">
            <summary>Determines if a number is prime using optimized algorithm.</summary>
            <param name="number">The number to check.</param>
            <returns>True if prime; otherwise, false.</returns>
        </member>
        <member name="T:DemoLib.StringUtility">
            <summary>Advanced string utilities for specialized operations.</summary>
        </member>
        <member name="M:DemoLib.StringUtility.Tokenize(System.String)">
            <summary>Tokenizes a string into meaningful segments.</summary>
            <param name="input">The input string to tokenize.</param>
            <returns>An array of tokens.</returns>
        </member>
        <member name="T:DemoLib.TextProcessor">
            <summary>Processes text documents with various algorithms.</summary>
        </member>
        <member name="M:DemoLib.TextProcessor.RemoveStopWords(System.String)">
            <summary>Removes common stop words from text for analysis.</summary>
            <param name="text">The text to process.</param>
            <returns>Text with stop words removed.</returns>
        </member>
    </members>
</doc>
'@ | Set-Content "$demoDir/DemoLib.xml"

# Get the repository root (two levels up from Demos/core/)
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$apilens = Join-Path $repoRoot "Solutions/ApiLens.Cli/bin/Debug/net9.0/apilens"
if ($IsWindows) { $apilens += ".exe" }

# Index the files
Write-Host "`n📚 Indexing documentation..." -ForegroundColor Yellow
Write-Command "apilens index $demoDir --clean --index $indexDir" "Index the XML documentation files"
& $apilens index $demoDir --clean --index "$indexDir"

# Demo queries
Write-Host "`n🔎 DEMO 1: Name search requires exact match" -ForegroundColor Green
Write-Command "apilens query 'Helper' --index $indexDir" "Search for 'Helper' (partial match won't work)"
Write-Host "Note: This returns no results because name searches need exact matches" -ForegroundColor DarkGray
& $apilens query "Helper" --index "$indexDir"

Write-Host "`n🔎 DEMO 2: Name search with exact match" -ForegroundColor Green
Write-Command "apilens query 'StringHelper' --index $indexDir" "Search for exact type name"
& $apilens query "StringHelper" --index "$indexDir"

Write-Host "`n🔎 DEMO 3: Content search with partial match" -ForegroundColor Green
Write-Command "apilens query 'string' --type content --index $indexDir" "Search in documentation content"
& $apilens query "string" --type content --index "$indexDir"

Write-Host "`n🔎 DEMO 4: Wildcard search in content" -ForegroundColor Green
Write-Command "apilens query 'string*' --type content --index $indexDir" "Wildcard search (* matches zero or more characters)"
& $apilens query "string*" --type content --index "$indexDir"

Write-Host "`n🔎 DEMO 5: Wildcard with single character" -ForegroundColor Green
Write-Command "apilens query 'utilit?' --type content --index $indexDir" "Single character wildcard (? matches exactly one character)"
Write-Host "Note: Finds 'utility' and 'utilities'" -ForegroundColor DarkGray
& $apilens query "utilit?" --type content --index "$indexDir"

Write-Host "`n🔎 DEMO 6: Fuzzy search" -ForegroundColor Green
Write-Command "apilens query 'tokenze~' --type content --index $indexDir" "Fuzzy search (~ finds similar terms)"
Write-Host "Note: Finds 'tokenize', 'tokenizes', etc." -ForegroundColor DarkGray
& $apilens query "tokenze~" --type content --index "$indexDir"

Write-Host "`n🔎 DEMO 7: Get JSON output (for LLMs)" -ForegroundColor Green
Write-Command "apilens query 'TextProcessor' --format json --index $indexDir" "Get results in JSON format"
& $apilens query "TextProcessor" --format json --index "$indexDir"

Write-Host "`n🔎 DEMO 8: Get specific member by ID" -ForegroundColor Green
Write-Command "apilens query 'M:DemoLib.StringHelper.Reverse(System.String)' --type id --index $indexDir" "Query by exact member ID"
& $apilens query "M:DemoLib.StringHelper.Reverse(System.String)" --type id --index "$indexDir"

Write-Host "`n🔎 DEMO 9: NEW - Deduplication Features" -ForegroundColor Green
Write-Host "ApiLens now properly handles duplicate entries across framework versions!" -ForegroundColor Gray

Write-Command "apilens list-types --assembly DemoLib --distinct true" "Show unique types (NEW: --distinct now works and defaults to true!)"
& $apilens list-types --assembly "DemoLib" --distinct true --index "$indexDir"

Write-Command "apilens list-types --assembly DemoLib --distinct false" "Compare: Show all versions (demonstrates why deduplication helps)"
& $apilens list-types --assembly "DemoLib" --distinct false --index "$indexDir"

Write-Host "`n🔎 DEMO 10: Specialized Commands Preview" -ForegroundColor Green
Write-Host "ApiLens has specialized commands for advanced analysis:" -ForegroundColor Gray

Write-Command "apilens examples --index $indexDir" "Find methods with code examples"
& $apilens examples --index "$indexDir" | Select-Object -First 3

Write-Command "apilens complexity --min-params 1 --index $indexDir" "Analyze method complexity"
& $apilens complexity --min-params 1 --index "$indexDir" | Select-Object -First 2

Write-Host "`n🔎 DEMO 11: JSON Processing for LLMs" -ForegroundColor Green
Write-Command "apilens query 'StringHelper' --format json --index $indexDir" "Get structured data for LLM processing"
$jsonData = & $apilens query "StringHelper" --format json --index "$indexDir"
if ($jsonData -and $jsonData -ne "[]") {
    try {
        $parsed = $jsonData | ConvertFrom-Json
        Write-Host "✓ JSON Response contains $($parsed.Count) member(s)" -ForegroundColor Green
        if ($parsed.Count -gt 0) {
            Write-Host "  First member: $($parsed[0].name) ($($parsed[0].memberType))" -ForegroundColor Gray
        }
    }
    catch {
        Write-Host "JSON parsing example (data available but not parsed)" -ForegroundColor Gray
    }
}
else {
    Write-Host "JSON response: $jsonData" -ForegroundColor Cyan
}

Write-Host "`n✨ Demo complete! ApiLens supports:" -ForegroundColor Cyan
Write-Host "   • Exact name matching" -ForegroundColor Cyan
Write-Host "   • Wildcard searches (* and ?) in content" -ForegroundColor Cyan  
Write-Host "   • Fuzzy searches (~) for similar terms" -ForegroundColor Cyan
Write-Host "   • Full Lucene query syntax (AND, OR, NOT, phrases)" -ForegroundColor Cyan
Write-Host "   • 🆕 Smart deduplication (--distinct defaults to true)" -ForegroundColor Cyan
Write-Host "   • 🆕 Property type linking for richer metadata" -ForegroundColor Cyan
Write-Host "   • Specialized commands: examples, exceptions, complexity" -ForegroundColor Cyan
Write-Host "   • Multiple output formats: table, JSON, markdown" -ForegroundColor Cyan
Write-Host "   • Rich metadata extraction for LLM integration" -ForegroundColor Cyan
Write-Host "🤖 Perfect for LLMs to understand your APIs via MCP!" -ForegroundColor Cyan

# Cleanup
Write-Host "`n🧹 Cleaning up demo files..." -ForegroundColor Yellow

# Clean up demo data
if (Test-Path $demoDir) {
    try {
        Remove-Item -Path $demoDir -Recurse -Force -ErrorAction Stop
        Write-Host "✅ Demo data cleaned up successfully" -ForegroundColor Green
    }
    catch {
        Write-Host "⚠️ Could not remove demo directory: $_" -ForegroundColor Yellow
    }
}
else {
    Write-Host "✅ Demo directory already clean" -ForegroundColor Green
}

# Clean up index
if (Test-Path $indexDir) {
    try {
        Remove-Item -Path $indexDir -Recurse -Force -ErrorAction Stop
        Write-Host "✅ Demo index cleaned up successfully" -ForegroundColor Green
    }
    catch {
        Write-Host "⚠️ Could not remove index directory: $_" -ForegroundColor Yellow
    }
}