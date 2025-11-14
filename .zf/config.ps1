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
    }

    # Setup coverage output file
    $coverageOutput = Join-Path $CoverageDir "coverage.cobertura.xml"
    Remove-Item $coverageOutput -ErrorAction Ignore -Force

    # Build dotnet-coverage arguments (matching original ZeroFailed pattern)
    # .NET 10 fix: include --solution flag in the coverage args
    $dotnetCoverageArgs = @(
        "collect"
        "-o", $coverageOutput
        "-f", "cobertura"
        "dotnet"
        "test"
        "--solution"
    )

    # Build dotnet test arguments
    $dotnetTestArgs = @(
        "--configuration", $Configuration
        "--no-build"
        "--no-restore"
        "--verbosity", "normal"
        "--results-directory", $CoverageDir
        "--report-trx"
        "--report-trx-filename", "test-results.trx"
    )

    Write-Build Green "Running tests with code coverage..."
    Write-Build Gray "  Solution: $SolutionToBuild"
    Write-Build Gray "  Coverage output: $coverageOutput"

    # Run using original ZeroFailed invocation pattern:
    # dotnet-coverage collect -o file.xml -f cobertura dotnet test --solution /path/to.sln --configuration Release ...
    exec {
        & dotnet-coverage @dotnetCoverageArgs $SolutionToBuild @dotnetTestArgs
    }

    # Verify coverage file was created
    if (Test-Path $coverageOutput) {
        $fileSize = (Get-Item $coverageOutput).Length
        Write-Build Green "Code coverage report generated: $coverageOutput ($fileSize bytes)"
    } else {
        Write-Build Yellow "Warning: Code coverage file not found at $coverageOutput"
    }
}
