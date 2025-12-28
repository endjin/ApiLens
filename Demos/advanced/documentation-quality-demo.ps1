#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Demonstrates ApiLens documentation quality analysis features.

.DESCRIPTION
    Shows how to use documentation quality metrics to:
    - Assess documentation coverage
    - Find poorly documented types
    - Track documentation improvements
    - Generate quality reports
#>

param(
    [string]$IndexPath = ""
)

# Set default index path if not provided
if (-not $IndexPath) {
    $tmpBase = Join-Path ([System.IO.Path]::GetTempPath()) "apilens-demo"
    $IndexPath = Join-Path $tmpBase "indexes/doc-quality-index"
}

Write-Host "`n📊 ApiLens Documentation Quality Demo" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "Analyzing and tracking documentation quality metrics`n" -ForegroundColor Gray

# Ensure ApiLens is built
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$apilens = Join-Path $repoRoot "Solutions/ApiLens.Cli/bin/Debug/net10.0/apilens"
if ($IsWindows) { $apilens += ".exe" }

if (-not (Test-Path $apilens)) {
    Write-Host "Building ApiLens..." -ForegroundColor Yellow
    dotnet build (Join-Path $repoRoot "Solutions/ApiLens.Cli/ApiLens.Cli.csproj") --verbosity quiet
}

# Clean up previous index
if (Test-Path $IndexPath) {
    Remove-Item -Path $IndexPath -Recurse -Force
}

# Create sample documentation with varying quality levels
Write-Host "📚 Creating sample documentation with varying quality levels..." -ForegroundColor Yellow
$tmpBase = Join-Path ([System.IO.Path]::GetTempPath()) "apilens-demo"
$docsDir = Join-Path $tmpBase "docs/quality-demo-docs"
New-Item -ItemType Directory -Path $docsDir -Force | Out-Null

# Sample 1: Well-documented code
@'
<?xml version="1.0"?>
<doc>
    <assembly><name>WellDocumented</name></assembly>
    <members>
        <!-- Excellent documentation -->
        <member name="T:WellDocumented.DataProcessor">
            <summary>
            Processes and transforms data from various sources into a unified format.
            This class provides comprehensive data transformation capabilities.
            </summary>
            <remarks>
            The DataProcessor class is the core component of the data transformation pipeline.
            It supports multiple input formats including CSV, JSON, and XML, and can output
            to any registered format provider. The processor is thread-safe and optimized
            for large datasets.
            
            Key features:
            - Automatic format detection
            - Streaming processing for large files
            - Extensible format provider system
            - Built-in validation and error handling
            </remarks>
            <example>
            Basic usage:
            <code>
            var processor = new DataProcessor();
            processor.Configure(options => {
                options.EnableValidation = true;
                options.MaxConcurrency = 4;
            });
            
            var result = await processor.ProcessAsync("input.csv", "output.json");
            Console.WriteLine($"Processed {result.RecordCount} records");
            </code>
            </example>
            <seealso cref="T:WellDocumented.IDataSource"/>
            <seealso cref="T:WellDocumented.DataProcessorOptions"/>
        </member>
        
        <member name="M:WellDocumented.DataProcessor.ProcessAsync(System.String,System.String)">
            <summary>
            Asynchronously processes data from the input file to the output file.
            </summary>
            <param name="inputPath">The path to the input file. Supports relative and absolute paths.</param>
            <param name="outputPath">The path where the processed data will be saved.</param>
            <returns>A task representing the asynchronous operation, containing processing results.</returns>
            <exception cref="T:System.ArgumentNullException">Thrown when inputPath or outputPath is null.</exception>
            <exception cref="T:System.IO.FileNotFoundException">Thrown when the input file does not exist.</exception>
            <exception cref="T:WellDocumented.ProcessingException">Thrown when data processing fails.</exception>
            <remarks>
            This method automatically detects the input format and selects the appropriate
            output formatter based on the file extension. For custom formats, use the
            overload that accepts format providers.
            </remarks>
            <example>
            <code>
            try
            {
                var result = await processor.ProcessAsync("data.csv", "output.json");
                Console.WriteLine($"Success: {result.Success}");
                Console.WriteLine($"Records: {result.RecordCount}");
                Console.WriteLine($"Duration: {result.ProcessingTime}");
            }
            catch (ProcessingException ex)
            {
                Console.WriteLine($"Processing failed: {ex.Message}");
                foreach (var error in ex.Errors)
                {
                    Console.WriteLine($"  - Line {error.Line}: {error.Message}");
                }
            }
            </code>
            </example>
        </member>
        
        <member name="P:WellDocumented.DataProcessor.Statistics">
            <summary>
            Gets the processing statistics for the current session.
            </summary>
            <value>
            A ProcessingStatistics object containing metrics such as total records processed,
            error count, and performance metrics.
            </value>
            <remarks>
            Statistics are accumulated across all processing operations performed by this
            instance. Call ResetStatistics() to clear the accumulated data.
            </remarks>
        </member>
        
        <!-- Good documentation -->
        <member name="T:WellDocumented.ValidationRule">
            <summary>
            Represents a validation rule that can be applied to data during processing.
            </summary>
            <remarks>
            Validation rules are executed in the order they are added to the processor.
            Failed validations can either skip the record or halt processing based on configuration.
            </remarks>
        </member>
        
        <member name="M:WellDocumented.ValidationRule.Validate(System.Object)">
            <summary>
            Validates the provided data against this rule.
            </summary>
            <param name="data">The data to validate.</param>
            <returns>True if validation passes; otherwise, false.</returns>
            <example>
            <code>
            var rule = new ValidationRule(data => data != null);
            bool isValid = rule.Validate(myData);
            </code>
            </example>
        </member>
    </members>
</doc>
'@ | Set-Content "$docsDir/WellDocumented.xml"

# Sample 2: Poorly documented code
@'
<?xml version="1.0"?>
<doc>
    <assembly><name>PoorlyDocumented</name></assembly>
    <members>
        <!-- Minimal documentation -->
        <member name="T:PoorlyDocumented.Manager">
            <summary>Manages things.</summary>
        </member>
        
        <member name="M:PoorlyDocumented.Manager.DoWork">
            <summary>Does work.</summary>
        </member>
        
        <member name="M:PoorlyDocumented.Manager.Process(System.Object)">
            <!-- No documentation at all -->
        </member>
        
        <member name="T:PoorlyDocumented.Helper">
            <!-- No documentation -->
        </member>
        
        <member name="T:PoorlyDocumented.Utility">
            <summary>Utility class</summary>
        </member>
        
        <member name="M:PoorlyDocumented.Utility.Calculate(System.Int32,System.Int32)">
            <summary>Calculates</summary>
            <param name="a">a</param>
            <param name="b">b</param>
            <returns>result</returns>
        </member>
        
        <!-- Mediocre documentation -->
        <member name="T:PoorlyDocumented.Service">
            <summary>
            Provides service functionality for the application.
            </summary>
        </member>
        
        <member name="M:PoorlyDocumented.Service.Execute(System.String)">
            <summary>
            Executes the service with the given parameter.
            </summary>
            <param name="parameter">The parameter to use.</param>
            <returns>The result of the execution.</returns>
        </member>
    </members>
</doc>
'@ | Set-Content "$docsDir/PoorlyDocumented.xml"

# Sample 3: Mixed quality documentation
@'
<?xml version="1.0"?>
<doc>
    <assembly><name>MixedQuality</name></assembly>
    <members>
        <!-- Well documented public API -->
        <member name="T:MixedQuality.PublicApi">
            <summary>
            Provides the main public API for the library.
            This is the primary entry point for consumers.
            </summary>
            <remarks>
            The PublicApi class follows the facade pattern to provide
            a simplified interface to the complex internal subsystems.
            All methods are thread-safe unless otherwise noted.
            </remarks>
            <example>
            <code>
            var api = new PublicApi();
            var result = api.PerformOperation("test");
            </code>
            </example>
        </member>
        
        <member name="M:MixedQuality.PublicApi.PerformOperation(System.String)">
            <summary>
            Performs the specified operation.
            </summary>
            <param name="operation">The name of the operation to perform.</param>
            <returns>The result of the operation.</returns>
            <exception cref="T:System.ArgumentNullException">Thrown when operation is null.</exception>
            <exception cref="T:System.InvalidOperationException">Thrown when the operation is not supported.</exception>
        </member>
        
        <!-- Poorly documented internal classes -->
        <member name="T:MixedQuality.InternalHelper">
            <summary>Internal helper</summary>
        </member>
        
        <member name="M:MixedQuality.InternalHelper.DoSomething">
            <!-- No documentation -->
        </member>
        
        <!-- Moderately documented -->
        <member name="T:MixedQuality.Configuration">
            <summary>
            Configuration settings for the library.
            </summary>
        </member>
        
        <member name="P:MixedQuality.Configuration.Timeout">
            <summary>
            Gets or sets the timeout in milliseconds.
            </summary>
        </member>
        
        <member name="P:MixedQuality.Configuration.MaxRetries">
            <summary>
            Gets or sets the maximum number of retries.
            </summary>
        </member>
    </members>
</doc>
'@ | Set-Content "$docsDir/MixedQuality.xml"

# Index the documentation
Write-Host "`nIndexing documentation..." -ForegroundColor Yellow
Write-Host "Command: " -NoNewline -ForegroundColor DarkGray
Write-Host "apilens index $docsDir --clean --index $IndexPath" -ForegroundColor Yellow
& $apilens index $docsDir --clean --index $IndexPath | Out-Null
Write-Host "✅ Documentation indexed successfully!" -ForegroundColor Green

# Demo 1: Basic Documentation Statistics
Write-Host "`n`n🎯 DEMO 1: Documentation Statistics" -ForegroundColor Yellow
Write-Host "Getting overall documentation quality metrics" -ForegroundColor Gray

Write-Host "`n1.1 Get basic index statistics:" -ForegroundColor Magenta
Write-Host "Command: " -NoNewline -ForegroundColor DarkGray
Write-Host "apilens stats --index $IndexPath" -ForegroundColor Yellow
& $apilens stats --index $IndexPath

Write-Host "`n1.2 Get statistics with documentation quality metrics:" -ForegroundColor Magenta
Write-Host "Command: " -NoNewline -ForegroundColor DarkGray
Write-Host "apilens stats --doc-metrics --index $IndexPath" -ForegroundColor Yellow
& $apilens stats --doc-metrics --index $IndexPath

# Demo 2: Documentation Quality in JSON
Write-Host "`n`n🎯 DEMO 2: Structured Quality Metrics" -ForegroundColor Yellow
Write-Host "Getting documentation metrics in JSON format for analysis" -ForegroundColor Gray

Write-Host "`n2.1 Get documentation metrics in JSON:" -ForegroundColor Magenta
Write-Host "Command: " -NoNewline -ForegroundColor DarkGray
Write-Host "apilens stats --doc-metrics --format json --index $IndexPath" -ForegroundColor Yellow
$jsonStats = & $apilens stats --doc-metrics --format json --index $IndexPath

Write-Host "`n2.2 Parse and analyze metrics:" -ForegroundColor Magenta
if ($jsonStats) {
    try {
        $stats = $jsonStats | ConvertFrom-Json
        if ($stats.results.documentationMetrics) {
            $metrics = $stats.results.documentationMetrics
            Write-Host "`nDocumentation Quality Analysis:" -ForegroundColor Green
            Write-Host "  Total Members: $($metrics.totalMembers)" -ForegroundColor Gray
            Write-Host "  Documented: $($metrics.documentedMembers) ($([math]::Round($metrics.documentationCoverage * 100, 1))%)" -ForegroundColor Gray
            Write-Host "  Well Documented: $($metrics.wellDocumentedMembers) ($([math]::Round($metrics.wellDocumentedPercentage * 100, 1))%)" -ForegroundColor Gray
            Write-Host "  With Examples: $($metrics.membersWithExamples) ($([math]::Round($metrics.exampleCoverage * 100, 1))%)" -ForegroundColor Gray
            Write-Host "  Average Score: $([math]::Round($metrics.averageDocScore, 1))/100" -ForegroundColor Gray
            
            if ($metrics.poorlyDocumentedTypes -and $metrics.poorlyDocumentedTypes.Count -gt 0) {
                Write-Host "`n  ⚠️ Types Needing Attention:" -ForegroundColor Yellow
                foreach ($type in $metrics.poorlyDocumentedTypes) {
                    Write-Host "    - $type" -ForegroundColor Red
                }
            }
        }
    }
    catch {
        Write-Host "Could not parse JSON metrics" -ForegroundColor Gray
    }
}

# Demo 3: Finding Documentation Gaps
Write-Host "`n`n🎯 DEMO 3: Finding Documentation Gaps" -ForegroundColor Yellow
Write-Host "Identifying areas that need documentation improvement" -ForegroundColor Gray

Write-Host "`n3.1 Search for types with minimal documentation:" -ForegroundColor Magenta
Write-Host "Using content search to find poorly documented items" -ForegroundColor Gray
Write-Host "Command: " -NoNewline -ForegroundColor DarkGray
Write-Host "apilens query 'Manager OR Helper OR Utility' --type name --max 10 --index $IndexPath" -ForegroundColor Yellow
& $apilens query "Manager OR Helper OR Utility" --type name --max 10 --index $IndexPath

Write-Host "`n3.2 Check specific type documentation quality:" -ForegroundColor Magenta
Write-Host "Command: " -NoNewline -ForegroundColor DarkGray
Write-Host "apilens query 'Manager' --type name --format json --index $IndexPath" -ForegroundColor Yellow
$managerJson = & $apilens query "Manager" --type name --format json --index $IndexPath
if ($managerJson) {
    try {
        $result = $managerJson | ConvertFrom-Json
        if ($result.results -and $result.results.Count -gt 0) {
            $member = $result.results[0]
            Write-Host "`nDocumentation Score for $($member.name): $($member.documentationScore)/100" -ForegroundColor $(if ($member.documentationScore -lt 40) { "Red" } elseif ($member.documentationScore -lt 70) { "Yellow" } else { "Green" })
            
            if ($member.isDocumented) {
                Write-Host "  ✓ Has documentation" -ForegroundColor Green
            } else {
                Write-Host "  ✗ No documentation" -ForegroundColor Red
            }
            
            if ($member.isWellDocumented) {
                Write-Host "  ✓ Well documented" -ForegroundColor Green
            } else {
                Write-Host "  ✗ Not well documented" -ForegroundColor Yellow
            }
        }
    }
    catch {
        # Silently continue
    }
}

# Demo 4: Comparing Documentation Quality
Write-Host "`n`n🎯 DEMO 4: Comparing Documentation Quality" -ForegroundColor Yellow
Write-Host "Comparing documentation quality across assemblies" -ForegroundColor Gray

Write-Host "`n4.1 List types from well-documented assembly:" -ForegroundColor Magenta
Write-Host "Command: " -NoNewline -ForegroundColor DarkGray
Write-Host "apilens list-types --assembly 'WellDocumented' --index $IndexPath" -ForegroundColor Yellow
& $apilens list-types --assembly "WellDocumented" --index $IndexPath

Write-Host "`n4.2 List types from poorly-documented assembly:" -ForegroundColor Magenta
Write-Host "Command: " -NoNewline -ForegroundColor DarkGray
Write-Host "apilens list-types --assembly 'PoorlyDocumented' --index $IndexPath" -ForegroundColor Yellow
& $apilens list-types --assembly "PoorlyDocumented" --index $IndexPath

# Demo 5: Finding Examples
Write-Host "`n`n🎯 DEMO 5: Documentation with Examples" -ForegroundColor Yellow
Write-Host "Finding well-documented code with examples" -ForegroundColor Gray

Write-Host "`n5.1 Find all members with code examples:" -ForegroundColor Magenta
Write-Host "Command: " -NoNewline -ForegroundColor DarkGray
Write-Host "apilens examples --index $IndexPath" -ForegroundColor Yellow
& $apilens examples --index $IndexPath

Write-Host "`n5.2 Get examples with quality information in JSON:" -ForegroundColor Magenta
Write-Host "Command: " -NoNewline -ForegroundColor DarkGray
Write-Host "apilens examples --format json --max 2 --index $IndexPath" -ForegroundColor Yellow
$examplesJson = & $apilens examples --format json --max 2 --index $IndexPath
if ($examplesJson) {
    try {
        $examples = $examplesJson | ConvertFrom-Json
        if ($examples.results -and $examples.results.Count -gt 0) {
            Write-Host "`nMembers with Examples:" -ForegroundColor Green
            foreach ($item in $examples.results) {
                Write-Host "  $($item.memberInfo.fullName)" -ForegroundColor Gray
                Write-Host "    Documentation Score: $($item.memberInfo.documentationScore)/100" -ForegroundColor Cyan
                Write-Host "    Example Count: $($item.codeExamples.Count)" -ForegroundColor Cyan
            }
        }
    }
    catch {
        # Silently continue
    }
}

# Demo 6: Documentation Quality Report
Write-Host "`n`n🎯 DEMO 6: Documentation Quality Report" -ForegroundColor Yellow
Write-Host "Generating a comprehensive documentation quality report" -ForegroundColor Gray

Write-Host "`n📊 Documentation Quality Report" -ForegroundColor Cyan
Write-Host "================================" -ForegroundColor Cyan

# Get stats for report
$reportStats = & $apilens stats --doc-metrics --format json --index $IndexPath | ConvertFrom-Json

if ($reportStats.results.documentationMetrics) {
    $m = $reportStats.results.documentationMetrics
    
    # Calculate grades
    $overallGrade = if ($m.averageDocScore -ge 80) { "A" } 
                    elseif ($m.averageDocScore -ge 70) { "B" } 
                    elseif ($m.averageDocScore -ge 60) { "C" } 
                    elseif ($m.averageDocScore -ge 50) { "D" } 
                    else { "F" }
    
    $coverageGrade = if ($m.documentationCoverage -ge 0.9) { "A" } 
                     elseif ($m.documentationCoverage -ge 0.75) { "B" } 
                     elseif ($m.documentationCoverage -ge 0.6) { "C" } 
                     elseif ($m.documentationCoverage -ge 0.4) { "D" } 
                     else { "F" }
    
    Write-Host "`n📈 Overall Metrics:" -ForegroundColor Yellow
    Write-Host "  Overall Grade: $overallGrade (Score: $([math]::Round($m.averageDocScore, 1))/100)" -ForegroundColor $(if ($overallGrade -eq "A" -or $overallGrade -eq "B") { "Green" } elseif ($overallGrade -eq "C") { "Yellow" } else { "Red" })
    Write-Host "  Coverage Grade: $coverageGrade ($([math]::Round($m.documentationCoverage * 100, 1))% documented)" -ForegroundColor $(if ($coverageGrade -eq "A" -or $coverageGrade -eq "B") { "Green" } elseif ($coverageGrade -eq "C") { "Yellow" } else { "Red" })
    
    Write-Host "`n📊 Detailed Breakdown:" -ForegroundColor Yellow
    Write-Host "  ✓ Total API Members: $($m.totalMembers)" -ForegroundColor Gray
    Write-Host "  ✓ Documented: $($m.documentedMembers) ($([math]::Round($m.documentationCoverage * 100, 1))%)" -ForegroundColor Gray
    Write-Host "  ✓ Well Documented: $($m.wellDocumentedMembers) ($([math]::Round($m.wellDocumentedPercentage * 100, 1))%)" -ForegroundColor Gray
    Write-Host "  ✓ With Examples: $($m.membersWithExamples) ($([math]::Round($m.exampleCoverage * 100, 1))%)" -ForegroundColor Gray
    
    if ($m.poorlyDocumentedTypes -and $m.poorlyDocumentedTypes.Count -gt 0) {
        Write-Host "`n⚠️ Action Items:" -ForegroundColor Yellow
        Write-Host "  The following types need documentation improvement:" -ForegroundColor Gray
        foreach ($type in $m.poorlyDocumentedTypes | Select-Object -First 5) {
            Write-Host "    • $type" -ForegroundColor Red
        }
        if ($m.poorlyDocumentedTypes.Count -gt 5) {
            Write-Host "    ... and $($m.poorlyDocumentedTypes.Count - 5) more" -ForegroundColor Gray
        }
    }
    
    Write-Host "`n💡 Recommendations:" -ForegroundColor Yellow
    if ($m.documentationCoverage -lt 0.8) {
        Write-Host "  • Increase documentation coverage to at least 80%" -ForegroundColor Gray
    }
    if ($m.exampleCoverage -lt 0.3) {
        Write-Host "  • Add more code examples (currently only $([math]::Round($m.exampleCoverage * 100, 1))%)" -ForegroundColor Gray
    }
    if ($m.averageDocScore -lt 70) {
        Write-Host "  • Improve documentation quality by adding remarks and details" -ForegroundColor Gray
    }
    if ($m.wellDocumentedPercentage -lt 0.5) {
        Write-Host "  • Focus on comprehensive documentation for public APIs" -ForegroundColor Gray
    }
}

# Summary
Write-Host "`n`n✨ Summary of Documentation Quality Features" -ForegroundColor Cyan
Write-Host @"

📊 Documentation Scoring System:
   • 0-100 scale based on multiple factors
   • Summary: +30 points
   • Remarks: +20 points  
   • Examples: +20 points
   • Parameters: +10 points
   • Returns: +10 points
   • Exceptions: +10 points

📈 Quality Metrics:
   • Documentation Coverage: % of members with any documentation
   • Well Documented: % with comprehensive documentation (score ≥ 70)
   • Example Coverage: % of members with code examples
   • Average Score: Mean documentation score across all members

🎯 Key Capabilities:
   • Assess overall documentation quality
   • Identify poorly documented types
   • Track documentation improvements
   • Generate quality reports
   • Compare documentation across assemblies

💡 Use Cases:
   • Documentation audits
   • Quality gates in CI/CD
   • Tracking documentation debt
   • Prioritizing documentation efforts
   • Generating documentation reports

🤖 Integration Benefits:
   • JSON output for automated reporting
   • Metrics for documentation dashboards
   • Quality tracking over time
   • Integration with documentation tools
   • LLM-based documentation suggestions

The documentation quality features help maintain high-quality
API documentation and identify areas needing improvement.
"@ -ForegroundColor Gray

# Cleanup
Write-Host "`n🧹 Cleanup Options" -ForegroundColor Yellow
Write-Host "Would you like to remove the demo files?" -ForegroundColor Gray
Write-Host "  - Demo index: $IndexPath" -ForegroundColor Gray
Write-Host "  - Sample docs: $docsDir" -ForegroundColor Gray
Write-Host "`nCleanup? (y/N): " -NoNewline -ForegroundColor Yellow
$cleanup = Read-Host
if ($cleanup -eq 'y') {
    Write-Host "`nCleaning up..." -ForegroundColor Gray
    
    if (Test-Path $IndexPath) {
        Remove-Item -Path $IndexPath -Recurse -Force
        Write-Host "✓ Demo index removed" -ForegroundColor Green
    }
    
    if (Test-Path $docsDir) {
        Remove-Item -Path $docsDir -Recurse -Force
        Write-Host "✓ Sample docs removed" -ForegroundColor Green
    }
    
    Write-Host "`n✅ Cleanup complete" -ForegroundColor Green
}
else {
    Write-Host "`nDemo files retained for further exploration." -ForegroundColor Gray
}