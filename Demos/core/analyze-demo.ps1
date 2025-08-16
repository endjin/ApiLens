#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Demonstrates the ApiLens analyze command for project and solution analysis.

.DESCRIPTION
    Shows how to analyze .NET projects and solutions to discover and index
    their NuGet package dependencies and API documentation.
#>

Write-Host "`n📊 ApiLens Project Analysis Demo" -ForegroundColor Cyan
Write-Host "=================================" -ForegroundColor Cyan

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

# Get the repository root (two levels up from Demos/core/)
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$apilens = Join-Path $repoRoot "Solutions/ApiLens.Cli/bin/Debug/net9.0/apilens"
if ($IsWindows) { $apilens += ".exe" }

# Setup demo directories
$tmpBase = Join-Path ([System.IO.Path]::GetTempPath()) "apilens-analyze-demo"
$demoProject = Join-Path $tmpBase "SampleProject"
$indexDir = Join-Path $tmpBase "project-index"

Write-Host "`n📁 Creating sample .NET project..." -ForegroundColor Yellow
New-Item -ItemType Directory -Path $demoProject -Force | Out-Null

# Create a sample .csproj file
@'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <OutputType>Library</OutputType>
  </PropertyGroup>
  
  <ItemGroup>
    <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
    <PackageReference Include="Serilog" Version="3.1.1" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="8.0.0" />
  </ItemGroup>
</Project>
'@ | Set-Content "$demoProject/SampleProject.csproj"

# Create a sample solution file
@'
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio Version 17
VisualStudioVersion = 17.0.31903.59
MinimumVisualStudioVersion = 10.0.40219.1
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "SampleProject", "SampleProject.csproj", "{12345678-1234-1234-1234-123456789012}"
EndProject
Global
	GlobalSection(SolutionConfigurationPlatforms) = preSolution
		Debug|Any CPU = Debug|Any CPU
		Release|Any CPU = Release|Any CPU
	EndGlobalSection
	GlobalSection(ProjectConfigurationPlatforms) = postSolution
		{12345678-1234-1234-1234-123456789012}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{12345678-1234-1234-1234-123456789012}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{12345678-1234-1234-1234-123456789012}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{12345678-1234-1234-1234-123456789012}.Release|Any CPU.Build.0 = Release|Any CPU
	EndGlobalSection
EndGlobal
'@ | Set-Content "$demoProject/SampleProject.sln"

Write-Host "`n🔍 DEMO 1: Analyze a single project" -ForegroundColor Green
Write-Command "apilens analyze $demoProject/SampleProject.csproj --index $indexDir" "Analyze project and index discovered packages"
& $apilens analyze "$demoProject/SampleProject.csproj" --index "$indexDir"

Write-Host "`n🔍 DEMO 2: Analyze with transitive dependencies" -ForegroundColor Green
Write-Command "apilens analyze $demoProject/SampleProject.csproj --include-transitive --use-assets" "Include transitive dependencies (requires project.assets.json)"
Write-Host "Note: This would show all indirect dependencies if project.assets.json exists" -ForegroundColor DarkGray
& $apilens analyze "$demoProject/SampleProject.csproj" --include-transitive --use-assets

Write-Host "`n🔍 DEMO 3: Analyze a solution file" -ForegroundColor Green
Write-Command "apilens analyze $demoProject/SampleProject.sln" "Analyze all projects in a solution"
& $apilens analyze "$demoProject/SampleProject.sln"

Write-Host "`n🔍 DEMO 4: JSON output for programmatic use" -ForegroundColor Green
Write-Command "apilens analyze $demoProject/SampleProject.csproj --format json" "Get analysis results as JSON"
$jsonOutput = & $apilens analyze "$demoProject/SampleProject.csproj" --format json
if ($jsonOutput) {
    try {
        $parsed = $jsonOutput | ConvertFrom-Json
        Write-Host "`n📊 Analysis Summary (from JSON):" -ForegroundColor Cyan
        Write-Host "  • Path: $($parsed.Path)" -ForegroundColor Gray
        Write-Host "  • Type: $($parsed.Type)" -ForegroundColor Gray
        Write-Host "  • Total Packages: $($parsed.TotalPackages)" -ForegroundColor Gray
        Write-Host "  • Frameworks: $($parsed.Frameworks -join ', ')" -ForegroundColor Gray
    } catch {
        # Show raw output if JSON parsing fails
        Write-Host $jsonOutput
    }
}

Write-Host "`n🔍 DEMO 5: Markdown output for documentation" -ForegroundColor Green
Write-Command "apilens analyze $demoProject/SampleProject.csproj --format markdown" "Generate markdown report"
& $apilens analyze "$demoProject/SampleProject.csproj" --format markdown

Write-Host "`n🔍 DEMO 6: Clean and rebuild index" -ForegroundColor Green
Write-Command "apilens analyze $demoProject/SampleProject.csproj --clean --index $indexDir" "Clean existing index before analyzing"
& $apilens analyze "$demoProject/SampleProject.csproj" --clean --index "$indexDir"

# Now demonstrate using the indexed content
Write-Host "`n📚 DEMO 7: Query the indexed packages" -ForegroundColor Green
Write-Host "After analyzing, you can search the indexed API documentation:" -ForegroundColor Gray

Write-Command "apilens query 'JsonConvert' --index $indexDir" "Search for Newtonsoft.Json APIs"
& $apilens query "JsonConvert" --index "$indexDir" 2>$null

Write-Command "apilens query 'ILogger' --index $indexDir" "Search for Serilog APIs"
& $apilens query "ILogger" --index "$indexDir" 2>$null

Write-Command "apilens query 'ServiceCollection' --index $indexDir" "Search for DI container APIs"
& $apilens query "ServiceCollection" --index "$indexDir" 2>$null

Write-Host "`n✨ Analyze Command Features:" -ForegroundColor Cyan
Write-Host "   • Discovers all NuGet package references" -ForegroundColor Cyan
Write-Host "   • Supports .csproj, .fsproj, .vbproj, and .sln files" -ForegroundColor Cyan
Write-Host "   • Finds packages in local NuGet cache" -ForegroundColor Cyan
Write-Host "   • Indexes XML documentation automatically" -ForegroundColor Cyan
Write-Host "   • Handles both SDK-style and packages.config projects" -ForegroundColor Cyan
Write-Host "   • Resolves transitive dependencies (with --use-assets)" -ForegroundColor Cyan
Write-Host "   • Multiple output formats: table, JSON, markdown" -ForegroundColor Cyan
Write-Host "   • Integrates with existing ApiLens query capabilities" -ForegroundColor Cyan

# Real-world example using ApiLens itself
Write-Host "`n🚀 BONUS: Analyze ApiLens itself!" -ForegroundColor Green
Write-Command "apilens analyze ./Solutions/ApiLens.sln --index ./.index" "Analyze the ApiLens solution"
Write-Host "This would index all packages used by ApiLens for self-documentation!" -ForegroundColor Gray

# Cleanup
Write-Host "`n🧹 Cleaning up demo files..." -ForegroundColor Yellow
if (Test-Path $tmpBase) {
    try {
        Remove-Item -Path $tmpBase -Recurse -Force -ErrorAction Stop
        Write-Host "✅ Demo files cleaned up successfully" -ForegroundColor Green
    }
    catch {
        Write-Host "⚠️ Could not remove demo directory: $_" -ForegroundColor Yellow
    }
}

Write-Host "`n📖 For more information, run: apilens analyze --help" -ForegroundColor Cyan