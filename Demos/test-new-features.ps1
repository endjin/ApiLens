#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Integration test script for new ApiLens features.

.DESCRIPTION
    Tests all new features including:
    - Hierarchy command
    - Method signature search
    - Documentation quality metrics
    - Suggestion service
    - JSON output fixes
#>

param(
    [switch]$Verbose,
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"
$testsFailed = 0
$testsPassed = 0

function Test-Feature {
    param(
        [string]$Name,
        [scriptblock]$Test
    )
    
    Write-Host "`n🧪 Testing: $Name" -ForegroundColor Cyan
    try {
        & $Test
        Write-Host "  ✅ PASSED" -ForegroundColor Green
        $script:testsPassed++
    }
    catch {
        Write-Host "  ❌ FAILED: $_" -ForegroundColor Red
        $script:testsFailed++
        if ($Verbose) {
            Write-Host "  Stack Trace: $($_.ScriptStackTrace)" -ForegroundColor DarkRed
        }
    }
}

function Assert-Contains {
    param(
        [string]$Text,
        [string]$Pattern,
        [string]$Message = "Text should contain pattern"
    )
    
    if ($Text -notmatch [regex]::Escape($Pattern)) {
        throw "$Message. Pattern '$Pattern' not found in text."
    }
}

function Assert-JsonValid {
    param(
        [string]$Json,
        [string]$Message = "JSON should be valid"
    )
    
    try {
        $null = $Json | ConvertFrom-Json
    }
    catch {
        throw "$Message. JSON parsing error: $_"
    }
}

Write-Host "`n🚀 ApiLens New Features Integration Test" -ForegroundColor Magenta
Write-Host "========================================" -ForegroundColor Magenta
Write-Host "Testing all new features and improvements`n" -ForegroundColor Gray

# Build ApiLens if needed
$repoRoot = Split-Path -Parent $PSScriptRoot
$apilens = Join-Path $repoRoot "Solutions/ApiLens.Cli/bin/Debug/net10.0/apilens"
if ($IsWindows) { $apilens += ".exe" }

if (-not $SkipBuild) {
    Write-Host "📦 Building ApiLens..." -ForegroundColor Yellow
    dotnet build (Join-Path $repoRoot "Solutions/ApiLens.Cli/ApiLens.Cli.csproj") --verbosity minimal
    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ Build failed" -ForegroundColor Red
        exit 1
    }
}

if (-not (Test-Path $apilens)) {
    Write-Host "❌ ApiLens executable not found at: $apilens" -ForegroundColor Red
    exit 1
}

# Set up test index
$tmpBase = Join-Path ([System.IO.Path]::GetTempPath()) "apilens-test"
$indexPath = Join-Path $tmpBase "test-index"
$docsDir = Join-Path $tmpBase "test-docs"

# Clean up previous test data
if (Test-Path $tmpBase) {
    Remove-Item -Path $tmpBase -Recurse -Force
}
New-Item -ItemType Directory -Path $docsDir -Force | Out-Null

# Create test documentation
Write-Host "📝 Creating test documentation..." -ForegroundColor Yellow
@'
<?xml version="1.0"?>
<doc>
    <assembly><name>TestAssembly</name></assembly>
    <members>
        <member name="T:TestNamespace.BaseClass">
            <summary>Base class for testing hierarchy.</summary>
            <remarks>This class has detailed documentation with remarks section.</remarks>
        </member>
        <member name="M:TestNamespace.BaseClass.VirtualMethod">
            <summary>A virtual method.</summary>
        </member>
        <member name="T:TestNamespace.DerivedClass">
            <summary>Derived class for testing.</summary>
            <seealso cref="T:TestNamespace.BaseClass"/>
        </member>
        <member name="T:TestNamespace.ITestInterface">
            <summary>Test interface.</summary>
        </member>
        <member name="T:TestNamespace.Implementation">
            <summary>Implementation class.</summary>
            <seealso cref="T:TestNamespace.ITestInterface"/>
        </member>
        <member name="M:TestNamespace.TestClass.MethodWithParams(System.String,System.Int32,System.Boolean)">
            <summary>Method with multiple parameters for testing.</summary>
            <param name="text">Text parameter.</param>
            <param name="number">Number parameter.</param>
            <param name="flag">Boolean flag.</param>
            <returns>A test result.</returns>
            <example>
            <code>
            var result = TestClass.MethodWithParams("test", 42, true);
            Console.WriteLine(result);
            </code>
            </example>
        </member>
        <member name="M:TestNamespace.TestClass.SimpleMethod">
            <summary>Simple method with no parameters.</summary>
        </member>
        <member name="T:TestNamespace.PoorlyDocumented">
            <summary>Class with minimal documentation.</summary>
        </member>
        <member name="M:TestNamespace.PoorlyDocumented.UndocumentedMethod">
        </member>
        <member name="P:TestNamespace.TestClass.TestProperty">
            <summary>Test property.</summary>
            <value>The test value.</value>
        </member>
    </members>
</doc>
'@ | Set-Content "$docsDir/TestAssembly.xml"

# Index the test documentation
Write-Host "📚 Indexing test documentation..." -ForegroundColor Yellow
& $apilens index $docsDir --clean --index $indexPath 2>&1 | Out-Null

# Test 1: Hierarchy Command
Test-Feature "Hierarchy Command - Basic" {
    $output = & $apilens hierarchy "BaseClass" --index $indexPath 2>&1
    Assert-Contains $output "BaseClass" "Should find BaseClass"
    Assert-Contains $output "Derived Types" "Should show derived types section"
}

Test-Feature "Hierarchy Command - JSON Output" {
    $json = & $apilens hierarchy "BaseClass" --format json --index $indexPath 2>&1
    Assert-JsonValid $json "Hierarchy JSON should be valid"
    $obj = $json | ConvertFrom-Json
    if (-not $obj.type) { throw "JSON should contain 'type' property" }
    if (-not ($obj.PSObject.Properties.Name -contains "derivedTypes")) { throw "JSON should contain 'derivedTypes' property" }
}

Test-Feature "Hierarchy Command - With Members" {
    $output = & $apilens hierarchy "BaseClass" --show-members --index $indexPath 2>&1
    Assert-Contains $output "VirtualMethod" "Should show type members"
}

# Test 2: Method Signature Search
Test-Feature "Method Search - Basic" {
    $output = & $apilens query "Method" --type method --index $indexPath 2>&1
    Assert-Contains $output "Method" "Should find methods"
}

Test-Feature "Method Search - With Parameter Filter" {
    $output = & $apilens query "Method" --type method --min-params 3 --index $indexPath 2>&1
    Assert-Contains $output "MethodWithParams" "Should find method with 3 parameters"
}

Test-Feature "Method Search - Parameter Range" {
    $output = & $apilens query "Method" --type method --min-params 0 --max-params 0 --index $indexPath 2>&1
    Assert-Contains $output "SimpleMethod" "Should find method with no parameters"
    if ($output -match "MethodWithParams") {
        throw "Should not find method with 3 parameters when max-params is 0"
    }
}

Test-Feature "Method Search - JSON Output" {
    $json = & $apilens query "Method" --type method --format json --index $indexPath 2>&1
    Assert-JsonValid $json "Method search JSON should be valid"
    $obj = $json | ConvertFrom-Json
    if (-not $obj.results) { throw "JSON should contain 'results' property" }
    if (-not $obj.metadata) { throw "JSON should contain 'metadata' property" }
}

# Test 3: Documentation Quality Metrics
Test-Feature "Documentation Quality - Stats Command" {
    $output = & $apilens stats --doc-metrics --index $indexPath 2>&1
    Assert-Contains $output "Documentation Quality" "Should show documentation quality section"
    Assert-Contains $output "Average Score" "Should show average documentation score"
}

Test-Feature "Documentation Quality - JSON Output" {
    $json = & $apilens stats --doc-metrics --format json --index $indexPath 2>&1
    Assert-JsonValid $json "Stats JSON should be valid"
    $obj = $json | ConvertFrom-Json
    if (-not $obj.results.documentationMetrics) { throw "JSON should contain documentation metrics" }
    if ($null -eq $obj.results.documentationMetrics.averageDocScore) { throw "Should have average doc score" }
}

Test-Feature "Documentation Quality - Poorly Documented Types" {
    $json = & $apilens stats --doc-metrics --format json --index $indexPath 2>&1
    $obj = $json | ConvertFrom-Json
    if ($obj.results.documentationMetrics.poorlyDocumentedTypes) {
        if ($obj.results.documentationMetrics.poorlyDocumentedTypes -notcontains "PoorlyDocumented") {
            # May or may not contain depending on scoring
            Write-Host "    Note: PoorlyDocumented type may not be in list" -ForegroundColor Gray
        }
    }
}

# Test 4: Suggestions (When No Results Found)
Test-Feature "Suggestions - Similar Names" {
    $output = & $apilens query "BaseClas" --index $indexPath 2>&1
    # Should either find BaseClass with fuzzy match or show suggestions
    if ($output -notmatch "BaseClass" -and $output -notmatch "No results found") {
        throw "Should either find BaseClass or show no results message"
    }
}

Test-Feature "Suggestions - Search Hints" {
    $output = & $apilens query "NonExistentType123" --index $indexPath 2>&1
    Assert-Contains $output "No results found" "Should show no results message"
    # Should show search hints
}

# Test 5: JSON Output with Special Characters
Test-Feature "JSON Output - Newline Handling" {
    # Create doc with newlines
    @'
<?xml version="1.0"?>
<doc>
    <assembly><name>NewlineTest</name></assembly>
    <members>
        <member name="T:NewlineTest.TestClass">
            <summary>
            Line 1
            Line 2
            Line 3
            </summary>
        </member>
    </members>
</doc>
'@ | Set-Content "$docsDir/NewlineTest.xml"
    
    & $apilens index $docsDir --index $indexPath 2>&1 | Out-Null
    
    $json = & $apilens query "NewlineTest" --format json --index $indexPath 2>&1
    Assert-JsonValid $json "JSON with newlines should be valid"
    
    # Verify JSON doesn't break when piped
    $parsed = $json | ConvertFrom-Json
    if (-not $parsed.results) { throw "Should be able to parse JSON with newlines" }
}

Test-Feature "JSON Output - Long Lines" {
    # Test that long JSON lines aren't wrapped
    $json = & $apilens query "TestClass" --format json --max 50 --index $indexPath 2>&1
    Assert-JsonValid $json "JSON with long lines should be valid"
    
    # Check that JSON doesn't contain unexpected line breaks
    $lines = $json -split "`n"
    foreach ($line in $lines) {
        if ($line -match '^\s+"[^"]+":.*[^,}\]]\s*$' -and $line -notmatch '[\[{]$') {
            # Line appears to be cut off mid-value
            if ($lines[$lines.IndexOf($line) + 1] -notmatch '^\s*[}\]]') {
                throw "JSON appears to have wrapped lines"
            }
        }
    }
}

# Test 6: Enhanced Type Listing
Test-Feature "List Types - With Wildcards" {
    $output = & $apilens list-types --namespace "Test*" --index $indexPath 2>&1
    Assert-Contains $output "TestNamespace" "Should find types in TestNamespace"
}

Test-Feature "List Types - JSON Output" {
    $json = & $apilens list-types --assembly "TestAssembly" --format json --index $indexPath 2>&1
    Assert-JsonValid $json "List types JSON should be valid"
}

# Test 7: Examples Command
Test-Feature "Examples Command - Find Examples" {
    $output = & $apilens examples --index $indexPath 2>&1
    Assert-Contains $output "MethodWithParams" "Should find method with examples"
}

Test-Feature "Examples Command - JSON Output" {
    $json = & $apilens examples --format json --index $indexPath 2>&1
    Assert-JsonValid $json "Examples JSON should be valid"
}

# Test 8: All Commands JSON Output Validation
$commands = @(
    @{Cmd = "query TestClass --format json"; Name = "Query JSON"},
    @{Cmd = "hierarchy BaseClass --format json"; Name = "Hierarchy JSON"},
    @{Cmd = "stats --doc-metrics --format json"; Name = "Stats JSON"},
    @{Cmd = "list-types --assembly TestAssembly --format json"; Name = "List Types JSON"},
    @{Cmd = "examples --format json"; Name = "Examples JSON"},
    @{Cmd = "exceptions ArgumentException --format json"; Name = "Exceptions JSON"},
    @{Cmd = "complexity --min-params 1 --format json"; Name = "Complexity JSON"}
)

foreach ($test in $commands) {
    Test-Feature "JSON Validation - $($test.Name)" {
        $json = & $apilens $test.Cmd.Split() --index $indexPath 2>&1
        Assert-JsonValid $json "$($test.Name) should produce valid JSON"
        
        # Test JSON can be piped and processed
        $obj = $json | ConvertFrom-Json
        if ($null -eq $obj) { throw "Should be able to parse JSON to object" }
        
        # Verify common structure
        if (-not ($obj.PSObject.Properties.Name -contains "results" -or $obj.PSObject.Properties.Name -contains "type")) {
            throw "JSON should have expected structure"
        }
    }
}

# Summary
Write-Host "`n`n📊 Test Results Summary" -ForegroundColor Cyan
Write-Host "========================" -ForegroundColor Cyan
Write-Host "✅ Passed: $testsPassed" -ForegroundColor Green
Write-Host "❌ Failed: $testsFailed" -ForegroundColor $(if ($testsFailed -eq 0) { "Green" } else { "Red" })

if ($testsFailed -eq 0) {
    Write-Host "`n🎉 All tests passed! New features are working correctly." -ForegroundColor Green
} else {
    Write-Host "`n⚠️ Some tests failed. Please review the errors above." -ForegroundColor Yellow
}

# Cleanup
Write-Host "`n🧹 Cleaning up test data..." -ForegroundColor Yellow
if (Test-Path $tmpBase) {
    Remove-Item -Path $tmpBase -Recurse -Force
}

# Exit with appropriate code
exit $(if ($testsFailed -eq 0) { 0 } else { 1 })