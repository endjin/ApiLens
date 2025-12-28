#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Demonstrates ApiLens NuGet cache scanning and version-aware querying features.

.DESCRIPTION
    This script showcases how to:
    - Index NuGet cache packages
    - Query with version information
    - Compare different versions of the same API
    - Find the latest versions of packages

.EXAMPLE
    ./demo-nuget-cache.ps1
#>

param(
    [string]$IndexPath = "",
    [switch]$SkipBuild
)

# Set default index path if not provided
if (-not $IndexPath) {
    $tmpBase = Join-Path ([System.IO.Path]::GetTempPath()) "apilens-demo"
    $IndexPath = Join-Path $tmpBase "indexes/nuget-index"
}

$ErrorActionPreference = "Stop"

# Colors for output
function Write-Header {
    param([string]$Message)
    Write-Host "`n$Message" -ForegroundColor Cyan
    Write-Host ("-" * $Message.Length) -ForegroundColor Cyan
}

function Write-Step {
    param([string]$Message)
    Write-Host "`n→ $Message" -ForegroundColor Green
}

function Write-Command {
    param([string]$Command)
    Write-Host "`n$ $Command" -ForegroundColor Yellow
}

# Build the project if not skipped
if (-not $SkipBuild) {
    Write-Header "Building ApiLens"
    $repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
    $csproj = Join-Path $repoRoot "Solutions/ApiLens.Cli/ApiLens.Cli.csproj"
    Write-Command "dotnet build $csproj"
    dotnet build $csproj --verbosity minimal
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Build failed"
        exit 1
    }
}

# Set up paths
$apilens = Join-Path $PSScriptRoot "../../Solutions/ApiLens.Cli/bin/Debug/net10.0/apilens"
if ($IsWindows -or $env:OS -eq "Windows_NT") {
    $apilens = "$apilens.exe"
}

if (-not (Test-Path $apilens)) {
    Write-Error "ApiLens executable not found at: $apilens"
    Write-Host "Please build the project first or remove -SkipBuild flag"
    exit 1
}

Write-Header "ApiLens NuGet Cache Scanning Demo"
Write-Host "This demo shows how to index and query NuGet packages with version information"

# Clean up any existing index
if (Test-Path $IndexPath) {
    Write-Step "Cleaning existing index"
    Remove-Item $IndexPath -Recurse -Force
}

# Step 1: Show current NuGet cache location
Write-Header "Step 1: Discovering NuGet Cache Location"
$nugetCache = if ($env:NUGET_PACKAGES) {
    $env:NUGET_PACKAGES
} else {
    Join-Path $HOME ".nuget/packages"
}
Write-Host "NuGet cache location: $nugetCache"

# Check if cache exists and has packages
if (Test-Path $nugetCache) {
    $packageCount = (Get-ChildItem -Path $nugetCache -Directory | Measure-Object).Count
    Write-Host "Found $packageCount packages in cache"
    
    # Show some example packages
    Write-Step "Sample packages in cache:"
    Get-ChildItem -Path $nugetCache -Directory | 
        Select-Object -First 5 | 
        ForEach-Object { Write-Host "  - $($_.Name)" }
} else {
    Write-Warning "NuGet cache not found at $nugetCache"
    Write-Host "You may need to restore some NuGet packages first"
}

# Step 2: Index specific NuGet packages
Write-Header "Step 2: Indexing NuGet Packages"

# First, let's create a sample project to ensure we have some packages
$tempProject = Join-Path ([System.IO.Path]::GetTempPath()) "apilens-demo-$(Get-Random)"
Write-Step "Creating temporary project to ensure sample packages"
New-Item -ItemType Directory -Path $tempProject -Force | Out-Null

@"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
    <PackageReference Include="System.Text.Json" Version="8.0.0" />
    <PackageReference Include="Microsoft.Extensions.Logging" Version="8.0.0" />
  </ItemGroup>
</Project>
"@ | Out-File -FilePath (Join-Path $tempProject "demo.csproj")

Write-Command "dotnet restore"
Push-Location $tempProject
dotnet restore --verbosity quiet
Pop-Location
Remove-Item $tempProject -Recurse -Force

# Now index specific packages
Write-Step "Indexing Newtonsoft.Json from NuGet cache"
$newtonsoftPath = Join-Path $nugetCache "newtonsoft.json"
if (Test-Path $newtonsoftPath) {
    Write-Command "$apilens index `"$newtonsoftPath`" --index `"$IndexPath`" --pattern `"**/*.xml`""
    & "$apilens" index "$newtonsoftPath" --index "$IndexPath" --pattern "**/*.xml"
} else {
    Write-Warning "Newtonsoft.Json not found in cache"
}

Write-Step "Indexing System.Text.Json from NuGet cache"
$systemTextJsonPath = Join-Path $nugetCache "system.text.json"
if (Test-Path $systemTextJsonPath) {
    Write-Command "$apilens index `"$systemTextJsonPath`" --index `"$IndexPath`" --pattern `"**/*.xml`""
    & "$apilens" index "$systemTextJsonPath" --index "$IndexPath" --pattern "**/*.xml"
} else {
    Write-Warning "System.Text.Json not found in cache"
}

# Step 3: Query with version information
Write-Header "Step 3: Querying APIs with Version Information"

Write-Step "Search for JsonSerializer (shows version info in table)"
Write-Command "$apilens query JsonSerializer --index `"$IndexPath`""
& "$apilens" query JsonSerializer --index "$IndexPath"

Write-Step "Search for JsonConvert with JSON output (includes all version fields)"
Write-Command "$apilens query JsonConvert --index `"$IndexPath`" --format json | ConvertFrom-Json"
try {
    $jsonOutput = & "$apilens" query JsonConvert --index "$IndexPath" --format json
    if ($jsonOutput -and $jsonOutput -ne "No results found." -and $jsonOutput -ne "[]") {
        $results = $jsonOutput | ConvertFrom-Json -ErrorAction Stop
        $results | ForEach-Object {
            Write-Host "`nAPI: $($_.fullName)"
            Write-Host "  Package: $($_.packageId) v$($_.packageVersion)"
            Write-Host "  Framework: $($_.targetFramework)"
            Write-Host "  From NuGet: $($_.isFromNuGetCache)"
        }
    } else {
        Write-Host "  No results found for JsonConvert" -ForegroundColor Yellow
    }
} catch {
    Write-Host "  Error parsing JSON output: $_" -ForegroundColor Red
    Write-Host "  Raw output: $jsonOutput" -ForegroundColor DarkGray
}

# Step 4: Content search across versions
Write-Header "Step 4: Content Search Across Package Versions"

Write-Step "Search for APIs that handle 'DateTime' across all versions"
Write-Command "$apilens query DateTime --type content --index `"$IndexPath`" --max 5"
& "$apilens" query DateTime --type content --index "$IndexPath" --max 5

# Step 5: Markdown output with version sections
Write-Header "Step 5: Detailed API Information with Versions"

Write-Step "Get detailed info for a specific type"
Write-Command "$apilens query `"Newtonsoft.Json.JsonSerializer`" --index `"$IndexPath`" --format markdown"
& "$apilens" query "Newtonsoft.Json.JsonSerializer" --index "$IndexPath" --format markdown

# Step 6: Statistics showing packages
Write-Header "Step 6: Index Statistics"

Write-Command "$apilens stats --index `"$IndexPath`""
& "$apilens" stats --index "$IndexPath"

# Step 7: Advanced scenarios
Write-Header "Step 7: Advanced Scenarios"

Write-Step "Find all APIs from a specific package version"
Write-Command "$apilens query `"Newtonsoft.Json`" --type assembly --index `"$IndexPath`" --max 10"
& "$apilens" query "Newtonsoft.Json" --type assembly --index "$IndexPath" --max 10

Write-Step "Search for extension methods in specific framework"
Write-Command "$apilens query `"extension method`" --type content --index `"$IndexPath`" --format table"
& "$apilens" query "extension method" --type content --index "$IndexPath" --format table

# Cleanup option
Write-Header "Demo Complete!"
Write-Host "`nIndex created at: $IndexPath"
Write-Host "You can explore the index further with these commands:"
Write-Host "  - List all types: $apilens query * --type content --max 20"
Write-Host "  - Find by namespace: $apilens query `"System.Text.Json`" --type namespace"
Write-Host "  - Complex search: $apilens query `"async AND Task`" --type content"

$cleanup = Read-Host "`nClean up the demo index? (y/N)"
if ($cleanup -eq 'y') {
    Remove-Item $IndexPath -Recurse -Force
    Write-Host "Index cleaned up."
}