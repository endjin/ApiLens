#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Demonstrates ApiLens specialized query capabilities for LLMs.

.DESCRIPTION
    Shows the new query commands: examples, exceptions, and complexity analysis.
#>

param(
    [string]$IndexPath = ""
)

# Set default index path if not provided
if (-not $IndexPath) {
    $tmpBase = Join-Path ([System.IO.Path]::GetTempPath()) "apilens-demo"
    $IndexPath = Join-Path $tmpBase "indexes/specialized-query-index"
}

Write-Host "`n🔍 ApiLens Specialized Queries Demo" -ForegroundColor Cyan
Write-Host "===================================" -ForegroundColor Cyan
Write-Host "Demonstrating enhanced query capabilities for LLM integration`n" -ForegroundColor Gray

# Ensure ApiLens is built
if (-not (Test-Path "./Solutions/ApiLens.Cli/bin/Debug/net9.0/apilens*")) {
    Write-Host "Building ApiLens..." -ForegroundColor Yellow
    dotnet build ./Solutions/ApiLens.Cli/ApiLens.Cli.csproj --verbosity quiet
}

$apilens = "./Solutions/ApiLens.Cli/bin/Debug/net9.0/apilens"
if ($IsWindows) { $apilens += ".exe" }

# Clean up previous index
if (Test-Path $IndexPath) {
    Remove-Item -Path $IndexPath -Recurse -Force
}

# Use the rich metadata documentation from previous demo
Write-Host "📚 Using rich metadata documentation..." -ForegroundColor Yellow
$tmpBase = Join-Path ([System.IO.Path]::GetTempPath()) "apilens-demo"
$docsDir = Join-Path $tmpBase "docs/rich-metadata-docs"

if (-not (Test-Path $docsDir)) {
    Write-Host "Creating sample documentation..." -ForegroundColor Yellow
    New-Item -ItemType Directory -Path $docsDir -Force | Out-Null
    
    # Create the same sample files as in Rich-Metadata-Demo.ps1
    @'
<?xml version="1.0"?>
<doc>
    <assembly><name>DataProcessing.Core</name></assembly>
    <members>
        <member name="T:DataProcessing.Core.CsvParser">
            <summary>Provides methods for parsing CSV files with advanced options.</summary>
            <example>
            Basic usage:
            <code>
            var parser = new CsvParser();
            var data = parser.ParseFile("data.csv");
            </code>
            </example>
            <example>
            Advanced usage with options:
            <code>
            var options = new CsvOptions 
            { 
                Delimiter = '\t',
                HasHeaders = true,
                SkipEmptyLines = true
            };
            var parser = new CsvParser(options);
            var data = parser.ParseFile("data.tsv");
            
            foreach (var row in data)
            {
                Console.WriteLine($"Name: {row["Name"]}, Value: {row["Value"]}");
            }
            </code>
            </example>
        </member>
        <member name="M:DataProcessing.Core.CsvParser.ParseFile(System.String)">
            <summary>Parses a CSV file and returns the data as a collection of rows.</summary>
            <param name="filePath">The path to the CSV file to parse.</param>
            <returns>A collection of dictionaries where each dictionary represents a row.</returns>
            <exception cref="T:System.IO.FileNotFoundException">Thrown when the specified file does not exist.</exception>
            <exception cref="T:DataProcessing.Core.CsvParseException">Thrown when the CSV format is invalid or cannot be parsed.</exception>
            <example>
            <code>
            try
            {
                var data = parser.ParseFile("customers.csv");
                Console.WriteLine($"Loaded {data.Count} records");
            }
            catch (CsvParseException ex)
            {
                Console.WriteLine($"Parse error at line {ex.LineNumber}: {ex.Message}");
            }
            </code>
            </example>
        </member>
        <member name="M:DataProcessing.Core.CsvParser.ValidateFormat(System.String,DataProcessing.Core.CsvOptions)">
            <summary>Validates that a CSV file conforms to the expected format.</summary>
            <param name="filePath">The path to the CSV file to validate.</param>
            <param name="options">The CSV parsing options to use for validation.</param>
            <returns>True if the file is valid; otherwise, false.</returns>
            <exception cref="T:System.ArgumentNullException">Thrown when filePath or options is null.</exception>
            <exception cref="T:System.IO.IOException">Thrown when there is an I/O error reading the file.</exception>
            <seealso cref="M:DataProcessing.Core.CsvParser.ParseFile(System.String)"/>
            <seealso cref="T:DataProcessing.Core.CsvOptions"/>
        </member>
    </members>
</doc>
'@ | Set-Content "$docsDir/DataProcessing.Core.xml"

    @'
<?xml version="1.0"?>
<doc>
    <assembly><name>Security.Validation</name></assembly>
    <members>
        <member name="T:Security.Validation.InputValidator">
            <summary>
            Provides comprehensive input validation for security-critical operations.
            All methods in this class throw exceptions for invalid input to ensure fail-fast behavior.
            </summary>
            <remarks>
            This validator is designed for high-security environments where input validation
            is critical. It performs multiple checks including length, format, and content validation.
            </remarks>
        </member>
        <member name="M:Security.Validation.InputValidator.ValidateEmail(System.String,System.Boolean)">
            <summary>Validates an email address according to RFC 5322 standards.</summary>
            <param name="email">The email address to validate. Must not be null or empty.</param>
            <param name="allowInternational">Whether to allow international domain names (IDN).</param>
            <returns>The normalized email address if valid.</returns>
            <exception cref="T:System.ArgumentNullException">Thrown when email is null.</exception>
            <exception cref="T:System.ArgumentException">Thrown when email is empty or contains only whitespace.</exception>
            <exception cref="T:Security.Validation.InvalidEmailException">Thrown when the email format is invalid.</exception>
            <exception cref="T:Security.Validation.DomainBlockedException">Thrown when the email domain is on the block list.</exception>
        </member>
        <member name="M:Security.Validation.InputValidator.ValidatePassword(System.String,Security.Validation.PasswordPolicy)">
            <summary>Validates a password against a configurable security policy.</summary>
            <param name="password">The password to validate.</param>
            <param name="policy">The password policy to enforce.</param>
            <exception cref="T:System.ArgumentNullException">Thrown when password or policy is null.</exception>
            <exception cref="T:Security.Validation.WeakPasswordException">
            Thrown when the password does not meet the minimum security requirements:
            - Length less than policy minimum
            - Missing required character types
            - Contains common patterns
            - Found in breach databases
            </exception>
            <example>
            <code>
            var policy = new PasswordPolicy
            {
                MinLength = 12,
                RequireUppercase = true,
                RequireLowercase = true,
                RequireDigits = true,
                RequireSpecialChars = true,
                CheckBreachDatabase = true
            };
            
            try
            {
                InputValidator.ValidatePassword(userInput, policy);
                // Password is valid
            }
            catch (WeakPasswordException ex)
            {
                Console.WriteLine($"Password rejected: {ex.Reason}");
                foreach (var issue in ex.Issues)
                {
                    Console.WriteLine($"- {issue}");
                }
            }
            </code>
            </example>
        </member>
    </members>
</doc>
'@ | Set-Content "$docsDir/Security.Validation.xml"
}

# Index the documentation
Write-Host "`nIndexing documentation..." -ForegroundColor Yellow
Write-Host "Command: " -NoNewline -ForegroundColor DarkGray
Write-Host "apilens index $docsDir --clean --index $IndexPath" -ForegroundColor Yellow
& $apilens index $docsDir --clean --index $IndexPath | Out-Null
Write-Host "✅ Documentation indexed successfully!" -ForegroundColor Green

# Demo 1: Code Examples Command
Write-Host "`n`n🎯 DEMO 1: Code Examples Command" -ForegroundColor Yellow
Write-Host "Finding and displaying methods with code examples" -ForegroundColor Gray

Write-Host "`n1.1 List all methods with code examples:" -ForegroundColor Magenta
Write-Host "Command: " -NoNewline -ForegroundColor DarkGray
Write-Host "apilens examples --index $IndexPath --max 5" -ForegroundColor Yellow
& $apilens examples --index $IndexPath --max 5

Write-Host "`n1.2 Search for specific code patterns:" -ForegroundColor Magenta
Write-Host "Command: " -NoNewline -ForegroundColor DarkGray
Write-Host "apilens examples 'parser.ParseFile' --index $IndexPath" -ForegroundColor Yellow
& $apilens examples "parser.ParseFile" --index $IndexPath

Write-Host "`n1.3 Search for error handling patterns (JSON format for automation):" -ForegroundColor Magenta
Write-Host "Command: " -NoNewline -ForegroundColor DarkGray
Write-Host "apilens examples 'catch' --format json --index $IndexPath" -ForegroundColor Yellow
& $apilens examples "catch" --format json --index $IndexPath

# Demo 2: Exceptions Command
Write-Host "`n`n🎯 DEMO 2: Exceptions Command" -ForegroundColor Yellow
Write-Host "Finding methods that throw specific exceptions" -ForegroundColor Gray

Write-Host "`n2.1 Find methods that throw ArgumentNullException:" -ForegroundColor Magenta
Write-Host "Command: " -NoNewline -ForegroundColor DarkGray
Write-Host "apilens exceptions 'System.ArgumentNullException' --index $IndexPath" -ForegroundColor Yellow
& $apilens exceptions "System.ArgumentNullException" --index $IndexPath

Write-Host "`n2.2 Get detailed exception information (Markdown format):" -ForegroundColor Magenta
Write-Host "Command: " -NoNewline -ForegroundColor DarkGray
Write-Host "apilens exceptions 'System.IO.IOException' --format markdown --details --index $IndexPath" -ForegroundColor Yellow
& $apilens exceptions "System.IO.IOException" --format markdown --details --index $IndexPath

# Demo 3: Complexity Command
Write-Host "`n`n🎯 DEMO 3: Complexity Command" -ForegroundColor Yellow
Write-Host "Analyzing method complexity and parameter counts" -ForegroundColor Gray

Write-Host "`n3.1 Find methods with multiple parameters:" -ForegroundColor Magenta
Write-Host "Command: " -NoNewline -ForegroundColor DarkGray
Write-Host "apilens complexity --min-params 2 --index $IndexPath" -ForegroundColor Yellow
& $apilens complexity --min-params 2 --index $IndexPath

Write-Host "`n3.2 Analyze complexity with statistics (Markdown format for reports):" -ForegroundColor Magenta
Write-Host "Command: " -NoNewline -ForegroundColor DarkGray
Write-Host "apilens complexity --min-complexity 1 --stats --format markdown --index $IndexPath" -ForegroundColor Yellow
& $apilens complexity --min-complexity 1 --stats --format markdown --index $IndexPath

Write-Host "`n3.3 Find methods within parameter range:" -ForegroundColor Magenta
Write-Host "Command: " -NoNewline -ForegroundColor DarkGray
Write-Host "apilens complexity --min-params 1 --max-params 2 --sort params --index $IndexPath" -ForegroundColor Yellow
& $apilens complexity --min-params 1 --max-params 2 --sort params --index $IndexPath

# Demo 4: Structured Output Formats for LLM Integration
Write-Host "`n`n🎯 DEMO 4: Structured Output Formats for LLM Integration" -ForegroundColor Yellow
Write-Host "Demonstrating JSON, Markdown, and Table formats for automated processing" -ForegroundColor Gray

Write-Host "`n4.1 JSON Format - Examples Command (LLM-friendly structured data):" -ForegroundColor Magenta
Write-Host "Command: " -NoNewline -ForegroundColor DarkGray
Write-Host "apilens examples --format json --max 1 --index $IndexPath" -ForegroundColor Yellow
$jsonExamples = & $apilens examples --format json --max 1 --index $IndexPath
Write-Host $jsonExamples -ForegroundColor Cyan

Write-Host "`n4.2 JSON Format - Complexity Analysis with Statistics:" -ForegroundColor Magenta
Write-Host "Command: " -NoNewline -ForegroundColor DarkGray
Write-Host "apilens complexity --min-complexity 1 --format json --stats --max 2 --index $IndexPath" -ForegroundColor Yellow
$jsonComplexity = & $apilens complexity --min-complexity 1 --format json --stats --max 2 --index $IndexPath
Write-Host $jsonComplexity -ForegroundColor Cyan

Write-Host "`n4.3 JSON Format - Exception Analysis:" -ForegroundColor Magenta
Write-Host "Command: " -NoNewline -ForegroundColor DarkGray
Write-Host "apilens exceptions 'ArgumentNullException' --format json --index $IndexPath" -ForegroundColor Yellow
$jsonExceptions = & $apilens exceptions "ArgumentNullException" --format json --index $IndexPath
Write-Host $jsonExceptions -ForegroundColor Cyan

Write-Host "`n4.4 Markdown Format - Documentation-Ready Output:" -ForegroundColor Magenta
Write-Host "Command: " -NoNewline -ForegroundColor DarkGray
Write-Host "apilens examples --format markdown --max 1 --index $IndexPath" -ForegroundColor Yellow
& $apilens examples --format markdown --max 1 --index $IndexPath

Write-Host "`n4.5 PowerShell JSON Processing Demo:" -ForegroundColor Magenta
if ($jsonExamples -and $jsonExamples -ne "[]") {
    try {
        $exampleData = $jsonExamples | ConvertFrom-Json
        if ($exampleData.Count -gt 0) {
            Write-Host "Parsing JSON response in PowerShell:" -ForegroundColor Green
            Write-Host "  Member ID: $($exampleData[0].memberInfo.id)" -ForegroundColor Gray
            Write-Host "  Member Name: $($exampleData[0].memberInfo.fullName)" -ForegroundColor Gray
            Write-Host "  Code Examples Count: $($exampleData[0].codeExamples.Count)" -ForegroundColor Gray
            Write-Host "  Search Pattern: $($exampleData[0].matchedPattern)" -ForegroundColor Gray
        }
    }
    catch {
        Write-Host "JSON parsing demonstration (no data available)" -ForegroundColor Gray
    }
}
else {
    Write-Host "JSON parsing demonstration (no examples found)" -ForegroundColor Gray
}

if ($jsonComplexity -and $jsonComplexity -ne "[]") {
    try {
        $complexityData = $jsonComplexity | ConvertFrom-Json
        if ($complexityData.results.Count -gt 0) {
            Write-Host "`nComplexity analysis from JSON:" -ForegroundColor Green
            Write-Host "  Criteria: $($complexityData.criteria)" -ForegroundColor Gray
            Write-Host "  Results Count: $($complexityData.results.Count)" -ForegroundColor Gray
            if ($complexityData.statistics) {
                Write-Host "  Average Complexity: $($complexityData.statistics.averageComplexity)" -ForegroundColor Gray
                Write-Host "  Max Parameters: $($complexityData.statistics.maxParameters)" -ForegroundColor Gray
            }
        }
    }
    catch {
        Write-Host "Complexity JSON parsing demonstration (no data available)" -ForegroundColor Gray
    }
}

# Demo 5: Combined LLM Workflow
Write-Host "`n`n🎯 DEMO 5: Advanced LLM Workflow Example" -ForegroundColor Yellow
Write-Host "How an LLM might use structured output to understand an API" -ForegroundColor Gray

Write-Host "`nScenario: LLM needs to understand error handling in the API" -ForegroundColor Cyan

Write-Host "`nStep 1 - Get structured exception data:" -ForegroundColor Green
Write-Host "Command: " -NoNewline -ForegroundColor DarkGray
Write-Host "apilens exceptions 'System.ArgumentNullException' --format json --index $IndexPath --max 2" -ForegroundColor Yellow
$exceptionData = & $apilens exceptions "System.ArgumentNullException" --format json --index $IndexPath --max 2
if ($exceptionData -and $exceptionData -ne "[]") {
    Write-Host "✓ Found structured exception data" -ForegroundColor Green
} else {
    Write-Host "No ArgumentNullException data found" -ForegroundColor Gray
}

Write-Host "`nStep 2 - Find error handling code examples:" -ForegroundColor Green
Write-Host "Command: " -NoNewline -ForegroundColor DarkGray
Write-Host "apilens examples 'catch' --format json --index $IndexPath --max 1" -ForegroundColor Yellow
$errorExamples = & $apilens examples "catch" --format json --index $IndexPath --max 1
if ($errorExamples -and $errorExamples -ne "[]") {
    Write-Host "✓ Found error handling examples" -ForegroundColor Green
} else {
    Write-Host "No error handling examples found" -ForegroundColor Gray
}

Write-Host "`nStep 3 - Analyze method complexity:" -ForegroundColor Green
Write-Host "Command: " -NoNewline -ForegroundColor DarkGray
Write-Host "apilens complexity --min-params 1 --format json --stats --index $IndexPath --max 3" -ForegroundColor Yellow
$complexityAnalysis = & $apilens complexity --min-params 1 --format json --stats --index $IndexPath --max 3
if ($complexityAnalysis -and $complexityAnalysis -ne "[]") {
    Write-Host "✓ Got complexity analysis with statistics" -ForegroundColor Green
} else {
    Write-Host "No complexity data found" -ForegroundColor Gray
}

# Show benefits
Write-Host "`n`n✨ Benefits for LLM Integration" -ForegroundColor Cyan
Write-Host @"

The specialized query commands with structured output formats enable LLMs to:

1. 📝 **Code Examples (apilens examples)**
   - Find working code snippets quickly
   - Learn API usage patterns from real examples
   - Understand best practices and common patterns
   
   Formats:
   • JSON: Structured data for automated processing
   • Markdown: Documentation-ready with syntax highlighting
   • Table: Human-readable console output

2. ⚠️ **Exception Analysis (apilens exceptions)**
   - Discover what errors methods can throw
   - Understand specific error conditions
   - Generate comprehensive error handling code
   
   Formats:
   • JSON: Member info + exception details + search context
   • Markdown: Detailed exception documentation
   • Table: Quick overview with optional details

3. 📊 **Complexity Analysis (apilens complexity)**
   - Identify simple vs complex methods
   - Understand method signatures at scale
   - Prioritize APIs by complexity for users
   - Get statistical analysis of codebases
   
   Formats:
   • JSON: Complexity metrics + optional statistics
   • Markdown: Analysis tables with statistical summaries
   • Table: Visual complexity overview

4. 🔄 **Structured Output Advantages**
   - JSON: Perfect for automation, scripting, and LLM consumption
   - Markdown: Ready for documentation generation and wikis
   - Table: Immediate human readability for development
   
   Each format provides the same rich data in the most appropriate structure
   for different use cases and integration scenarios.

5. 🤖 **LLM Workflow Integration**
   - Chain commands using JSON output for complex analysis
   - Parse structured responses programmatically
   - Build contextual knowledge from multiple query types
   - Generate documentation, tests, and code examples
   - Understand API patterns and best practices at scale

These enhanced commands provide targeted access to rich metadata
with flexible output formats, enabling better code generation,
API understanding, and automated documentation workflows.
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
    
    # Clean up index
    if (Test-Path $IndexPath) {
        try {
            Remove-Item -Path $IndexPath -Recurse -Force -ErrorAction Stop
            Write-Host "✓ Demo index removed: $IndexPath" -ForegroundColor Green
        }
        catch {
            Write-Host "⚠ Could not remove index: $_" -ForegroundColor Yellow
        }
    }
    
    # Clean up docs
    if (Test-Path $docsDir) {
        try {
            Remove-Item -Path $docsDir -Recurse -Force -ErrorAction Stop
            Write-Host "✓ Sample docs removed: $docsDir" -ForegroundColor Green
        }
        catch {
            Write-Host "⚠ Could not remove docs: $_" -ForegroundColor Yellow
        }
    }
    
    Write-Host "`n✅ Cleanup complete" -ForegroundColor Green
}
else {
    Write-Host "`nDemo files retained for further exploration." -ForegroundColor Gray
}