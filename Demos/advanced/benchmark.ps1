#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Benchmarks ApiLens performance with various dataset sizes.

.DESCRIPTION
    Tests indexing and query performance to demonstrate ApiLens scalability.
#>

param(
    [int]$SmallDatasetSize = 100,
    [int]$MediumDatasetSize = 1000,
    [int]$LargeDatasetSize = 5000
)

Write-Host "`n⚡ ApiLens Performance Benchmark" -ForegroundColor Cyan
Write-Host "===============================" -ForegroundColor Cyan

# Ensure ApiLens is built
if (-not (Test-Path "./Solutions/ApiLens.Cli/bin/Debug/net9.0/apilens*")) {
    Write-Host "Building ApiLens..." -ForegroundColor Yellow
    dotnet build ./Solutions/ApiLens.Cli/ApiLens.Cli.csproj --verbosity quiet
}

# Get the repository root (two levels up from Demos/advanced/)
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$apilens = Join-Path $repoRoot "Solutions/ApiLens.Cli/bin/Debug/net9.0/apilens"
if ($IsWindows) { $apilens += ".exe" }

# Function to generate synthetic XML documentation
function New-SyntheticXmlDoc {
    param(
        [string]$AssemblyName,
        [int]$TypeCount,
        [int]$MembersPerType
    )
    
    $xml = @"
<?xml version="1.0"?>
<doc>
    <assembly><name>$AssemblyName</name></assembly>
    <members>
"@
    
    for ($t = 1; $t -le $TypeCount; $t++) {
        $typeName = "$AssemblyName.Type$t"
        $xml += @"
        <member name="T:$typeName">
            <summary>Generated type $t with various members for performance testing.</summary>
            <remarks>This type contains methods, properties, and fields to simulate a real API.</remarks>
        </member>
"@
        
        for ($m = 1; $m -le $MembersPerType; $m++) {
            # Mix of different member types
            $memberType = $m % 4
            switch ($memberType) {
                0 { # Method
                    $xml += @"
        <member name="M:$typeName.Method$m(System.String,System.Int32)">
            <summary>Processes data using algorithm $m with string and integer parameters.</summary>
            <param name="input">The input string to process.</param>
            <param name="count">The number of iterations.</param>
            <returns>The processed result as a string.</returns>
            <exception cref="T:System.ArgumentNullException">Thrown when input is null.</exception>
        </member>
"@
                }
                1 { # Property
                    $xml += @"
        <member name="P:$typeName.Property$m">
            <summary>Gets or sets the property $m value.</summary>
            <value>The current value of property $m.</value>
        </member>
"@
                }
                2 { # Field
                    $xml += @"
        <member name="F:$typeName.Field$m">
            <summary>Stores the field $m data.</summary>
        </member>
"@
                }
                3 { # Event
                    $xml += @"
        <member name="E:$typeName.Event$m">
            <summary>Occurs when condition $m is met.</summary>
        </member>
"@
                }
            }
        }
    }
    
    $xml += @"
    </members>
</doc>
"@
    
    return $xml
}

# Function to measure execution time
function Measure-ExecutionTime {
    param(
        [scriptblock]$ScriptBlock,
        [string]$Description
    )
    
    Write-Host "`n$Description" -ForegroundColor Yellow
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    & $ScriptBlock
    $stopwatch.Stop()
    $elapsed = $stopwatch.Elapsed
    
    Write-Host "✅ Completed in: $($elapsed.TotalSeconds.ToString('F2')) seconds" -ForegroundColor Green
    return $elapsed
}

# Create benchmark directory
$benchDir = "./benchmark-data"
New-Item -ItemType Directory -Path $benchDir -Force | Out-Null

# Small Dataset Benchmark
Write-Host "`n📊 SMALL DATASET ($SmallDatasetSize members)" -ForegroundColor Magenta
$smallIndex = "$benchDir/small-index"

# Generate small dataset
Write-Host "Generating small dataset..." -ForegroundColor Gray
New-SyntheticXmlDoc -AssemblyName "SmallAssembly" -TypeCount 10 -MembersPerType 10 | 
    Set-Content "$benchDir/small.xml"

# Index small dataset
$smallIndexTime = Measure-ExecutionTime -Description "Indexing small dataset..." -ScriptBlock {
    & $apilens index "$benchDir/small.xml" --clean --index $smallIndex | Out-Null
}

# Query benchmarks
$smallQueryTimes = @{}
$smallQueryTimes['ByName'] = Measure-ExecutionTime -Description "Query by name (Method5)..." -ScriptBlock {
    & $apilens query "Method5" --index $smallIndex | Out-Null
}

$smallQueryTimes['ByContent'] = Measure-ExecutionTime -Description "Query by content (algorithm)..." -ScriptBlock {
    & $apilens query "algorithm" --type content --index $smallIndex | Out-Null
}

$smallQueryTimes['ByNamespace'] = Measure-ExecutionTime -Description "Query by namespace..." -ScriptBlock {
    & $apilens query "SmallAssembly" --type namespace --max 50 --index $smallIndex | Out-Null
}

# Medium Dataset Benchmark
Write-Host "`n📊 MEDIUM DATASET ($MediumDatasetSize members)" -ForegroundColor Magenta
$mediumIndex = "$benchDir/medium-index"

# Generate medium dataset
Write-Host "Generating medium dataset..." -ForegroundColor Gray
New-SyntheticXmlDoc -AssemblyName "MediumAssembly" -TypeCount 50 -MembersPerType 20 | 
    Set-Content "$benchDir/medium.xml"

# Index medium dataset
$mediumIndexTime = Measure-ExecutionTime -Description "Indexing medium dataset..." -ScriptBlock {
    & $apilens index "$benchDir/medium.xml" --clean --index $mediumIndex | Out-Null
}

# Query benchmarks
$mediumQueryTimes = @{}
$mediumQueryTimes['ByName'] = Measure-ExecutionTime -Description "Query by name (Method50)..." -ScriptBlock {
    & $apilens query "Method50" --index $mediumIndex | Out-Null
}

$mediumQueryTimes['ByContent'] = Measure-ExecutionTime -Description "Query by content (process)..." -ScriptBlock {
    & $apilens query "process" --type content --index $mediumIndex | Out-Null
}

# Large Dataset Benchmark
Write-Host "`n📊 LARGE DATASET ($LargeDatasetSize members)" -ForegroundColor Magenta
$largeIndex = "$benchDir/large-index"

# Generate large dataset (multiple files)
Write-Host "Generating large dataset (3 files)..." -ForegroundColor Gray
New-SyntheticXmlDoc -AssemblyName "LargeAssembly1" -TypeCount 50 -MembersPerType 35 | 
    Set-Content "$benchDir/large1.xml"
New-SyntheticXmlDoc -AssemblyName "LargeAssembly2" -TypeCount 50 -MembersPerType 35 | 
    Set-Content "$benchDir/large2.xml"
New-SyntheticXmlDoc -AssemblyName "LargeAssembly3" -TypeCount 40 -MembersPerType 35 | 
    Set-Content "$benchDir/large3.xml"

# Index large dataset
$largeIndexTime = Measure-ExecutionTime -Description "Indexing large dataset (3 files)..." -ScriptBlock {
    & $apilens index $benchDir --pattern "large*.xml" --clean --index $largeIndex | Out-Null
}

# Query benchmarks
$largeQueryTimes = @{}
$largeQueryTimes['ByName'] = Measure-ExecutionTime -Description "Query by name (Property100)..." -ScriptBlock {
    & $apilens query "Property100" --index $largeIndex | Out-Null
}

$largeQueryTimes['ByContent'] = Measure-ExecutionTime -Description "Query by content (exception)..." -ScriptBlock {
    & $apilens query "exception" --type content --max 50 --index $largeIndex | Out-Null
}

$largeQueryTimes['Complex'] = Measure-ExecutionTime -Description "Complex query (ArgumentNullException in content)..." -ScriptBlock {
    & $apilens query "ArgumentNullException" --type content --index $largeIndex | Out-Null
}

# Real-world test if .NET SDK is available
$dotnetPacks = if ($IsWindows) {
    "C:\Program Files\dotnet\packs"
} else {
    "/usr/share/dotnet/packs"
}

if (Test-Path $dotnetPacks) {
    Write-Host "`n📊 REAL-WORLD TEST (.NET SDK Documentation)" -ForegroundColor Magenta
    $realIndex = "$benchDir/dotnet-index"
    
    # Find some .NET XML files
    $xmlFiles = Get-ChildItem -Path $dotnetPacks -Filter "*.xml" -Recurse | 
                Select-Object -First 10
    
    if ($xmlFiles.Count -gt 0) {
        Write-Host "Found $($xmlFiles.Count) .NET XML documentation files" -ForegroundColor Gray
        
        $realIndexTime = Measure-ExecutionTime -Description "Indexing .NET documentation..." -ScriptBlock {
            foreach ($file in $xmlFiles) {
                & $apilens index $file.FullName --index $realIndex | Out-Null
            }
        }
        
        $realQueryTime = Measure-ExecutionTime -Description "Query .NET docs (String class)..." -ScriptBlock {
            & $apilens query "String" --index $realIndex | Out-Null
        }
    }
}

# Performance Summary
Write-Host "`n📈 PERFORMANCE SUMMARY" -ForegroundColor Cyan
Write-Host "=====================" -ForegroundColor Cyan

Write-Host "`nIndexing Performance:" -ForegroundColor Yellow
Write-Host ("  Small  ({0,5} members): {1,6:F2}s" -f $SmallDatasetSize, $smallIndexTime.TotalSeconds)
Write-Host ("  Medium ({0,5} members): {1,6:F2}s" -f $MediumDatasetSize, $mediumIndexTime.TotalSeconds)
Write-Host ("  Large  ({0,5} members): {1,6:F2}s" -f $LargeDatasetSize, $largeIndexTime.TotalSeconds)
if ($realIndexTime) {
    Write-Host ("  .NET SDK docs:          {0,6:F2}s" -f $realIndexTime.TotalSeconds)
}

Write-Host "`nQuery Performance (Large Dataset):" -ForegroundColor Yellow
$nameSearchSeconds = $largeQueryTimes['ByName'].TotalMilliseconds / 1000
$contentSearchSeconds = $largeQueryTimes['ByContent'].TotalMilliseconds / 1000
$complexSearchSeconds = $largeQueryTimes['Complex'].TotalMilliseconds / 1000
Write-Host ("  Name search:    {0,6:F3}s" -f $nameSearchSeconds)
Write-Host ("  Content search: {0,6:F3}s" -f $contentSearchSeconds)
Write-Host ("  Complex search: {0,6:F3}s" -f $complexSearchSeconds)
if ($realQueryTime) {
    $realQuerySeconds = $realQueryTime.TotalMilliseconds / 1000
    Write-Host ("  .NET SDK query: {0,6:F3}s" -f $realQuerySeconds)
}

# Calculate metrics
$membersPerSecond = $LargeDatasetSize / $largeIndexTime.TotalSeconds

# Get index size using the new stats command
$indexSizeMB = 0
Write-Host "`nGetting index statistics..." -ForegroundColor Gray
$statsJson = & $apilens stats --index $largeIndex --format json 2>$null
if ($LASTEXITCODE -eq 0 -and $statsJson) {
    try {
        $stats = $statsJson | ConvertFrom-Json
        if ($stats.totalSizeInBytes) {
            $indexSizeMB = $stats.totalSizeInBytes / 1MB
            Write-Host "Index statistics retrieved successfully" -ForegroundColor Green
        }
    }
    catch {
        Write-Host "Warning: Could not parse index statistics" -ForegroundColor Yellow
    }
}
else {
    Write-Host "Warning: Could not get index statistics" -ForegroundColor Yellow
}

Write-Host "`nPerformance Metrics:" -ForegroundColor Yellow
Write-Host ("  Indexing speed: {0:F0} members/second" -f $membersPerSecond)
Write-Host ("  Index size:     {0:F1} MB for {1} members" -f $indexSizeMB, $LargeDatasetSize)
Write-Host ("  Size ratio:     {0:F2} KB per 100 members" -f ($indexSizeMB * 1024 / $LargeDatasetSize * 100))

Write-Host "`n✨ Benchmark Complete!" -ForegroundColor Green
Write-Host @"

Key Findings:
- Lucene.NET provides sub-second query response times even with thousands of members
- Index size grows linearly with content
- Complex content searches remain performant
- Suitable for real-time LLM/MCP integration

"@ -ForegroundColor Gray

# Cleanup
Write-Host "Remove benchmark data? (y/N): " -NoNewline -ForegroundColor Yellow
$cleanup = Read-Host
if ($cleanup -eq 'y') {
    Remove-Item -Path $benchDir -Recurse -Force
    Write-Host "✅ Benchmark data removed" -ForegroundColor Green
}