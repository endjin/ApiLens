#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Demonstrates ApiLens rich metadata extraction capabilities.

.DESCRIPTION
    Shows how ApiLens extracts and indexes code examples, exceptions, parameters,
    and other rich metadata from .NET XML documentation.
#>

param(
    [string]$IndexPath = "/.tmp/indexes/rich-metadata-index"
)

Write-Host "`n🔍 ApiLens Rich Metadata Demo" -ForegroundColor Cyan
Write-Host "=============================" -ForegroundColor Cyan
Write-Host "Demonstrating enhanced metadata extraction capabilities`n" -ForegroundColor Gray

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

# Create sample documentation with rich metadata
Write-Host "📚 Creating sample XML documentation with rich metadata..." -ForegroundColor Yellow
$docsDir = "/.tmp/docs/rich-metadata-docs"
New-Item -ItemType Directory -Path $docsDir -Force | Out-Null

# Sample 1: Documentation with code examples
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

# Sample 2: Documentation with detailed parameter and exception info
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

# Index the documentation
Write-Host "`nIndexing documentation..." -ForegroundColor Yellow
& $apilens index $docsDir --clean --index $IndexPath | Out-Null
Write-Host "✅ Documentation indexed successfully!" -ForegroundColor Green

# Helper function to display results
function Show-Results {
    param(
        [string]$Title,
        [string]$Query,
        [string]$QueryType = "content"
    )
    
    Write-Host "`n📋 $Title" -ForegroundColor Magenta
    
    # Show the exact command
    $command = "apilens query '$Query' --type $QueryType --format table --index $IndexPath"
    Write-Host "Command: " -NoNewline -ForegroundColor DarkGray
    Write-Host "$command" -ForegroundColor Yellow
    
    & $apilens query $Query --type $QueryType --format table --index $IndexPath
}

# Demo 1: Search by code example content
Write-Host "`n`n🎯 DEMO 1: Searching by Code Example Content" -ForegroundColor Yellow
Write-Host "Finding APIs by searching for code patterns in their examples" -ForegroundColor Gray

Show-Results -Title "Search for CSV parsing examples" -Query "parser.ParseFile"
Show-Results -Title "Search for error handling examples" -Query "catch CsvParseException"

# Demo 2: Search by exception types
Write-Host "`n`n🎯 DEMO 2: Searching by Exception Types" -ForegroundColor Yellow
Write-Host "Finding methods that throw specific exceptions" -ForegroundColor Gray

Show-Results -Title "Methods that throw ArgumentNullException" -Query "ArgumentNullException"
Show-Results -Title "Security-related exceptions" -Query "SecurityException OR BlockedException"

# Demo 3: Search by parameter descriptions
Write-Host "`n`n🎯 DEMO 3: Searching by Parameter Descriptions" -ForegroundColor Yellow
Write-Host "Finding methods by their parameter documentation" -ForegroundColor Gray

Show-Results -Title "Methods with file path parameters" -Query "path to the CSV file"
Show-Results -Title "Methods with validation options" -Query "policy to enforce"

# Demo 4: Search for specific patterns in documentation
Write-Host "`n`n🎯 DEMO 4: Complex Documentation Searches" -ForegroundColor Yellow
Write-Host "Using Lucene query syntax for advanced searches" -ForegroundColor Gray

Show-Results -Title "High-security methods" -Query "high-security AND validation"
Show-Results -Title "Methods with RFC compliance" -Query "RFC*"

# Demo 5: Get detailed information about a specific member
Write-Host "`n`n🎯 DEMO 5: Detailed Member Information" -ForegroundColor Yellow
Write-Host "Retrieving full metadata for a specific API member" -ForegroundColor Gray

Write-Host "`nGetting details for CsvParser.ParseFile method:" -ForegroundColor Gray
$command = "apilens query 'ParseFile' --type name --format json --index $IndexPath"
Write-Host "Command: " -NoNewline -ForegroundColor DarkGray
Write-Host "$command" -ForegroundColor Yellow
$jsonOutput = & $apilens query "ParseFile" --type name --format json --index $IndexPath | ConvertFrom-Json

if ($jsonOutput -and $jsonOutput.Count -gt 0) {
    $member = $jsonOutput[0]
    
    Write-Host "`n📄 Member: $($member.fullName)" -ForegroundColor Cyan
    Write-Host "Summary: $($member.summary)" -ForegroundColor Gray
    
    if ($member.codeExamples -and $member.codeExamples.Count -gt 0) {
        Write-Host "`n📝 Code Examples:" -ForegroundColor Green
        foreach ($example in $member.codeExamples) {
            if ($example.description) {
                Write-Host "  $($example.description)" -ForegroundColor DarkGray
            }
            Write-Host "  ``````csharp" -ForegroundColor DarkGray
            $example.code -split "`n" | ForEach-Object { Write-Host "  $_" -ForegroundColor Gray }
            Write-Host "  ``````" -ForegroundColor DarkGray
        }
    }
    
    if ($member.exceptions -and $member.exceptions.Count -gt 0) {
        Write-Host "`n⚠️ Exceptions:" -ForegroundColor Yellow
        foreach ($exception in $member.exceptions) {
            Write-Host "  - $($exception.type)" -ForegroundColor Red
            if ($exception.condition) {
                Write-Host "    $($exception.condition)" -ForegroundColor Gray
            }
        }
    }
    
    if ($member.parameters -and $member.parameters.Count -gt 0) {
        Write-Host "`n📥 Parameters:" -ForegroundColor Blue
        foreach ($param in $member.parameters) {
            Write-Host "  - $($param.name) ($($param.type))" -ForegroundColor Cyan
            if ($param.description) {
                Write-Host "    $($param.description)" -ForegroundColor Gray
            }
        }
    }
    
    if ($member.returns) {
        Write-Host "`n📤 Returns:" -ForegroundColor Magenta
        Write-Host "  $($member.returns)" -ForegroundColor Gray
    }
}

# Show benefits for LLMs
Write-Host "`n`n✨ Benefits for LLM Integration" -ForegroundColor Cyan
Write-Host @"

The rich metadata extraction provides LLMs with:

1. 📝 **Code Examples** - Learn usage patterns and generate similar code
   - Multiple examples per method
   - Preserves formatting and structure
   - Searchable by code patterns

2. ⚠️ **Exception Information** - Better error handling recommendations
   - Full exception type names
   - Conditions that trigger exceptions
   - Searchable by exception type

3. 📥 **Parameter Details** - Understand method signatures
   - Parameter types and names
   - Detailed descriptions
   - Validation requirements

4. 📊 **Complexity Metrics** - Prioritize APIs by complexity
   - Parameter count
   - Documentation quality
   - Cyclomatic complexity estimates

5. 🔗 **Cross-References** - Navigate related APIs
   - See also references
   - Related types and methods
   - Build comprehensive understanding

This enables LLMs to provide more accurate code generation, better error
handling advice, and deeper API understanding.
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