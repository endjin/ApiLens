#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Test all demo scripts to ensure they work correctly.
.DESCRIPTION
    Runs through all demo scripts in the organized demos folder structure
    and verifies they execute without errors.
#>

param(
    [string]$Category = "all",  # all, core, nuget, advanced
    [switch]$Verbose
)

$ErrorActionPreference = "Continue"

# Get script root - works when script is run from any directory
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptRoot

Write-Host "`n🧪 Testing ApiLens Demo Scripts" -ForegroundColor Cyan
Write-Host "=================================" -ForegroundColor Cyan

# Define demo categories and scripts
$demoCategories = @{
    core = @(
        @{Name = "basic-usage.ps1"; Description = "Basic ApiLens usage demonstration"},
        @{Name = "quick-start.ps1"; Description = "Quick start guide for ApiLens"},
        @{Name = "version-info.ps1"; Description = "Version information display demo"}
    )
    nuget = @(
        @{Name = "nuget-basic.ps1"; Description = "Basic NuGet command usage"},
        @{Name = "nuget-command.ps1"; Description = "Full NuGet command features"; Timeout = $true},
        @{Name = "nuget-cache-indexing.ps1"; Description = "NuGet cache indexing demonstration"},
        @{Name = "nuget-scanner.ps1"; Description = "NuGet cache scanner functionality"},
        @{Name = "version-comparison.ps1"; Description = "Version comparison features"; Args = @("newtonsoft.json")}
    )
    advanced = @(
        @{Name = "rich-metadata.ps1"; Description = "Rich metadata extraction demo"},
        @{Name = "specialized-queries.ps1"; Description = "Specialized query demonstrations"},
        @{Name = "mcp-integration.ps1"; Description = "Model Context Protocol integration"},
        @{Name = "benchmark.ps1"; Description = "Performance benchmarking"; Skip = $true; Reason = "Benchmark takes too long for regular testing"}
    )
}

# Determine which demos to run
$demosToRun = @()
if ($Category -eq "all") {
    foreach ($cat in $demoCategories.Keys) {
        foreach ($demo in $demoCategories[$cat]) {
            $demo.Category = $cat
            $demosToRun += $demo
        }
    }
} elseif ($demoCategories.ContainsKey($Category)) {
    foreach ($demo in $demoCategories[$Category]) {
        $demo.Category = $Category
        $demosToRun += $demo
    }
} else {
    Write-Error "Invalid category: $Category. Valid options are: all, core, nuget, advanced"
    exit 1
}

Write-Host "Testing category: $Category" -ForegroundColor Yellow
Write-Host "Total demos to test: $($demosToRun.Count)" -ForegroundColor Yellow

$results = @()

foreach ($demo in $demosToRun) {
    $demoPath = Join-Path $scriptRoot $demo.Category $demo.Name
    
    Write-Host "`n📝 Testing: $($demo.Category)/$($demo.Name)" -ForegroundColor Yellow
    Write-Host "   $($demo.Description)" -ForegroundColor DarkGray
    
    if ($demo.Skip) {
        Write-Host "   ⏭️  Skipped: $($demo.Reason)" -ForegroundColor DarkYellow
        $results += @{Script = "$($demo.Category)/$($demo.Name)"; Status = "Skipped"; Duration = $null}
        continue
    }
    
    if (-not (Test-Path $demoPath)) {
        Write-Host "   ❌ File not found: $demoPath" -ForegroundColor Red
        $results += @{Script = "$($demo.Category)/$($demo.Name)"; Status = "NotFound"; Duration = $null}
        continue
    }
    
    try {
        $startTime = Get-Date
        
        # Change to repo root for demo execution
        Push-Location $repoRoot
        
        if ($demo.Timeout) {
            Write-Host "   (This demo lists many packages, testing first part only...)" -ForegroundColor DarkGray
            $output = & $demoPath 2>&1 | Select-Object -First 50
        } elseif ($demo.Args) {
            $output = & $demoPath @($demo.Args) 2>&1
        } else {
            $output = & $demoPath 2>&1
        }
        
        Pop-Location
        
        $duration = (Get-Date) - $startTime
        
        # Check for success indicators (improved error detection)
        $hasError = $output -match "error:|exception:|failed:|cannot:|unable:" | Where-Object { 
            $_ -notmatch "error handling|Handle errors|Found \d+ method.*throw|throw.*exception|exception.*method|method.*exception|method.*that throw|methods.*that throw|Found \d+ method.*with|Found \d+ interface|Found \d+ enum|Found \d+ attribute|Found \d+ class|Found \d+ type|Found \d+ property|Found \d+ namespace|Found \d+ event|Found \d+ field|Found \d+ constructor|Found \d+ delegate|method with code examples|methods with code examples|\d+ Error\(s\)|Failed Documents|won't throw exceptions|catch.*Exception|specific exceptions|throwing exceptions|ArgumentException|DivideByZeroException|without throwing" 
        }
        $hasVersionInfo = $output -match "\d+\.\d+\.\d+ \["
        $hasNuGetCommand = $output -match "apilens nuget"
        
        # Always show a preview of the output to verify commands are working
        if ($output -and $output.Count -gt 0) {
            Write-Host "`n   Output preview:" -ForegroundColor DarkGray
            $output | Select-Object -First 15 | ForEach-Object { Write-Host "   > $_" -ForegroundColor DarkGray }
            if ($output.Count -gt 15) {
                Write-Host "   > ... (output truncated)" -ForegroundColor DarkGray
            }
        }
        
        if (-not $hasError) {
            Write-Host "   ✅ Success" -ForegroundColor Green
            Write-Host "   Duration: $($duration.TotalSeconds.ToString('F1'))s" -ForegroundColor DarkGray
            if ($hasVersionInfo) {
                Write-Host "   ✓ Version info displayed" -ForegroundColor Green
            }
            if ($hasNuGetCommand) {
                Write-Host "   ✓ NuGet command used" -ForegroundColor Green
            }
            $results += @{Script = "$($demo.Category)/$($demo.Name)"; Status = "Success"; Duration = $duration}
        } else {
            Write-Host "   ❌ Failed" -ForegroundColor Red
            $errorLines = $output | Where-Object { $_ -match "error:|exception:" } | Select-Object -First 3
            foreach ($line in $errorLines) {
                Write-Host "      $line" -ForegroundColor Red
            }
            $results += @{Script = "$($demo.Category)/$($demo.Name)"; Status = "Failed"; Duration = $duration}
        }
    } catch {
        Write-Host "   ❌ Exception: $_" -ForegroundColor Red
        $results += @{Script = "$($demo.Category)/$($demo.Name)"; Status = "Exception"; Duration = $null}
    }
}

# Summary
Write-Host "`n📊 Summary" -ForegroundColor Cyan
Write-Host "============" -ForegroundColor Cyan

$successful = $results | Where-Object { $_.Status -eq "Success" }
$failed = $results | Where-Object { $_.Status -eq "Failed" -or $_.Status -eq "Exception" -or $_.Status -eq "NotFound" }
$skipped = $results | Where-Object { $_.Status -eq "Skipped" }

Write-Host "Total: $($results.Count) scripts" -ForegroundColor White
Write-Host "✅ Successful: $($successful.Count)" -ForegroundColor Green
Write-Host "❌ Failed: $($failed.Count)" -ForegroundColor Red
Write-Host "⏭️  Skipped: $($skipped.Count)" -ForegroundColor DarkYellow

if ($failed.Count -gt 0) {
    Write-Host "`nFailed scripts:" -ForegroundColor Red
    foreach ($fail in $failed) {
        Write-Host "  - $($fail.Script): $($fail.Status)" -ForegroundColor Red
    }
}

# Key feature verification (only if not in specific category mode)
if ($Category -eq "all" -or $Category -eq "nuget") {
    Write-Host "`n🔍 Feature Verification" -ForegroundColor Cyan
    Write-Host "======================" -ForegroundColor Cyan
    
    # Test specific features
    Write-Host "`nTesting NuGet command..." -ForegroundColor Yellow
    $apilensPath = Join-Path $repoRoot "Solutions/ApiLens.Cli/bin/Debug/net9.0/apilens"
    if ($IsWindows -or $env:OS -eq "Windows_NT") { 
        $apilensPath += ".exe" 
    }
    if (-not (Test-Path $apilensPath)) {
        Write-Host "⚠️  ApiLens executable not found at: $apilensPath" -ForegroundColor Yellow
    }
    
    if (Test-Path $apilensPath) {
        try {
            $nugetTest = & "$apilensPath" nuget --list --filter "newtonsoft.*" 2>&1 | Select-Object -First 5
            if ($nugetTest -match "newtonsoft.json") {
                Write-Host "✅ NuGet command works" -ForegroundColor Green
            } else {
                Write-Host "❌ NuGet command failed" -ForegroundColor Red
            }
        } catch {
            Write-Host "❌ NuGet command failed with error: $_" -ForegroundColor Red
        }
        
        Write-Host "`nTesting version info in queries..." -ForegroundColor Yellow
        $tempIndex = Join-Path ([System.IO.Path]::GetTempPath()) "test-version-$(Get-Random)"
        try {
            & "$apilensPath" nuget --filter "newtonsoft.*" --latest-only --index "$tempIndex" 2>&1 | Out-Null
            $queryTest = & "$apilensPath" query JsonSerializer --index "$tempIndex" 2>&1
            Remove-Item $tempIndex -Recurse -Force -ErrorAction SilentlyContinue
        } catch {
            Write-Host "❌ Version info test failed with error: $_" -ForegroundColor Red
            if (Test-Path $tempIndex) { Remove-Item $tempIndex -Recurse -Force -ErrorAction SilentlyContinue }
        }
        
        if ($queryTest -match "\d+\.\d+\.\d+ \[") {
            Write-Host "✅ Version info displayed in queries" -ForegroundColor Green
        } else {
            Write-Host "❌ Version info not displayed" -ForegroundColor Red
        }
    } else {
        Write-Host "⚠️  ApiLens not built - skipping feature verification" -ForegroundColor Yellow
        Write-Host "   Run 'dotnet build ./Solutions/ApiLens.Cli/ApiLens.Cli.csproj' first" -ForegroundColor DarkGray
    }
}

Write-Host "`n✨ Demo test complete!" -ForegroundColor Green

# Exit with error code if any failed
if ($failed.Count -gt 0) {
    exit 1
}