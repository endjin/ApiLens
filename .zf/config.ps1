<#
This example demonstrates a software build process using the 'ZeroFailed.Build.DotNet' extension
to provide the features needed when building a .NET solutions.
#>

$zerofailedExtensions = @(
    @{
        # References the extension from its GitHub repository. If not already installed, use latest version from 'main' will be downloaded.
        Name = "ZeroFailed.Build.DotNet"
        GitRepository = "https://github.com/zerofailed/ZeroFailed.Build.DotNet"
        GitRef = "main"
    }
)

# Load the tasks and process
. ZeroFailed.tasks -ZfPath $here/.zf


#
# Build process control options
#
$SkipInit = $false
$SkipVersion = $false
$SkipBuild = $false
$CleanBuild = $Clean
$SkipTest = $false
$SkipTestReport = $false
$SkipAnalysis = $false
$SkipPackage = $false

#
# Build process configuration
#
$SolutionToBuild = (Resolve-Path (Join-Path $here ".\Solutions\ApiLens.slnx")).Path
$ProjectsToPublish = @()
$NuSpecFilesToPackage = @()
$NugetPublishSource = property ZF_NUGET_PUBLISH_SOURCE "$here/_local-nuget-feed"
$IncludeAssembliesInCodeCoverage = "ApiLens*"


# Synopsis: Build, Test and Package
task . FullBuild

#
# Build Process Extensibility Points - uncomment and implement as required
#

# task RunFirst {}
# task PreInit {}
# task PostInit {}
# task PreVersion {}
# task PostVersion {}
# task PreBuild {}
# task PostBuild {}
# task PreTest {}
# task PostTest {}
# task PreTestReport {}
# task PostTestReport {}
# task PreAnalysis {}
# task PostAnalysis {}
# task PrePackage {}
# task PostPackage {}
# task PrePublish {}
# task PostPublish {}
# task RunLast {}

#
# Task Overrides for .NET 10 Compatibility
#

# Override RunTestsWithDotNetCoverage to support .NET 10 Microsoft.Testing.Platform
# .NET 10 requires '--solution' flag instead of positional solution argument
task RunTestsWithDotNetCoverage {
    if ($SkipTest) {
        Write-Build Yellow "Skipping tests (SkipTest = true)"
        return
    }

    # Ensure dotnet-coverage tool is available
    $dotnetCoverageExe = Get-Command dotnet-coverage -ErrorAction SilentlyContinue
    if (-not $dotnetCoverageExe) {
        Write-Build Yellow "dotnet-coverage tool not found. Installing..."
        exec { dotnet tool install --global dotnet-coverage }
        $dotnetCoverageExe = Get-Command dotnet-coverage
    }

    # Setup coverage output file
    $coverageFile = Join-Path $CoverageDir "coverage.cobertura.xml"

    # Build dotnet test arguments with .NET 10 compatible syntax
    # .NET 10 with Microsoft.Testing.Platform requires --solution flag
    $dotnetTestArgs = @(
        "test"
        "--solution"
        $SolutionToBuild
        "--configuration"
        $Configuration
        "--no-build"
        "--no-restore"
        "--verbosity"
        "normal"
    )

    Write-Build Green "Running tests with code coverage..."
    Write-Build Gray "  Solution: $SolutionToBuild"
    Write-Build Gray "  Coverage output: $coverageFile"

    # Run tests with code coverage using dotnet-coverage
    $dotnetCoverageArgs = @(
        "collect"
        "--output-format"
        "cobertura"
        "--output"
        $coverageFile
    )

    # Add include filter for assemblies if specified
    if ($IncludeAssembliesInCodeCoverage) {
        $dotnetCoverageArgs += "--include-assemblies"
        $dotnetCoverageArgs += $IncludeAssembliesInCodeCoverage
    }

    $dotnetCoverageArgs += "--"
    $dotnetCoverageArgs += "dotnet"
    $dotnetCoverageArgs += $dotnetTestArgs

    exec {
        & dotnet-coverage @dotnetCoverageArgs
    }

    # Verify coverage file was created
    if (Test-Path $coverageFile) {
        $fileSize = (Get-Item $coverageFile).Length
        Write-Build Green "Code coverage report generated: $coverageFile ($fileSize bytes)"
    } else {
        Write-Build Yellow "Warning: Code coverage file not found at $coverageFile"
    }
}
