#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Demonstrates ApiLens capabilities for indexing and querying .NET API documentation.

.DESCRIPTION
    This script showcases various ApiLens features including:
    - Building and setting up the application
    - Indexing XML documentation files
    - Performing different types of queries
    - Exploring type hierarchies and relationships
    - Using different output formats
    - MCP integration scenarios

.NOTES
    Requires .NET 9 SDK and PowerShell 7+
#>

param(
    [string]$WorkingDirectory = "",
    [switch]$SkipBuild,
    [switch]$Verbose
)

# Set default working directory if not provided
if ([string]::IsNullOrEmpty($WorkingDirectory)) {
    $WorkingDirectory = $PSScriptRoot
}

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# Colors for output
$colors = @{
    Header = "Cyan"
    Section = "Yellow"
    Success = "Green"
    Error = "Red"
    Info = "Gray"
    Example = "Magenta"
    Warning = "Yellow"
}

function Write-Header {
    param([string]$Text)
    Write-Host "`n$("=" * 80)" -ForegroundColor $colors.Header
    Write-Host $Text.ToUpper() -ForegroundColor $colors.Header
    Write-Host "$("=" * 80)`n" -ForegroundColor $colors.Header
}

function Write-Section {
    param([string]$Text)
    Write-Host "`n--- $Text ---" -ForegroundColor $colors.Section
}

function Write-Example {
    param([string]$Command, [string]$Description)
    Write-Host "`n$Description" -ForegroundColor $colors.Info
    Write-Host "$ $Command" -ForegroundColor $colors.Example
}

function Invoke-ApiLens {
    param([string[]]$ArgumentList)
    
    # Get the repo root (two levels up from script location)
    $repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
    $apiLensPath = Join-Path $repoRoot "Solutions/ApiLens.Cli/bin/Debug/net10.0/apilens"
    if ($IsWindows) {
        $apiLensPath += ".exe"
    }
    
    if (-not (Test-Path $apiLensPath)) {
        Write-Error "ApiLens not found at: $apiLensPath. Please run with -SkipBuild:$false"
    }
    
    if ($Verbose) {
        Write-Host "Executing: $apiLensPath $($ArgumentList -join ' ')" -ForegroundColor $colors.Info
    }
    
    & $apiLensPath $ArgumentList
}

# Change to working directory
Push-Location $WorkingDirectory
try {
    Write-Header "ApiLens Demo Script"
    Write-Host "This script demonstrates the capabilities of ApiLens for querying .NET API documentation." -ForegroundColor $colors.Info

    # Step 1: Build the application
    if (-not $SkipBuild) {
        Write-Section "Building ApiLens"
        Write-Host "Building the application in Release mode..." -ForegroundColor $colors.Info
        
        $repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
        $csprojPath = Join-Path $repoRoot "Solutions/ApiLens.Cli/ApiLens.Cli.csproj"
        dotnet build $csprojPath --verbosity minimal
        if ($LASTEXITCODE -ne 0) {
            throw "Build failed"
        }
        Write-Host "Build completed successfully!" -ForegroundColor $colors.Success
    }

    # Step 2: Prepare sample data
    Write-Section "Preparing Sample Data"
    $tmpBase = Join-Path ([System.IO.Path]::GetTempPath()) "apilens-demo"
    $dataDir = Join-Path $tmpBase "data"
    $indexDir = Join-Path $tmpBase "indexes/demo-index"
    
    if (-not (Test-Path $dataDir)) {
        New-Item -ItemType Directory -Path $dataDir -Force | Out-Null
    }
    
    # Create sample XML documentation files
    Write-Host "Creating sample XML documentation files..." -ForegroundColor $colors.Info
    
    # Sample 1: Enhanced Mathematical Library with Code Examples
    @'
<?xml version="1.0"?>
<doc>
    <assembly>
        <name>SampleLibrary</name>
    </assembly>
    <members>
        <member name="T:SampleLibrary.Calculator">
            <summary>
            Provides basic mathematical operations with thread-safe implementations.
            </summary>
            <remarks>
            This class implements common arithmetic operations with comprehensive error handling
            and performance optimizations. All operations are thread-safe and suitable for
            concurrent environments.
            </remarks>
            <example>
            Basic usage:
            <code>
            var calculator = new Calculator();
            var result = calculator.Add(10.5, 20.3);
            Console.WriteLine($"Result: {result}");
            </code>
            </example>
        </member>
        <member name="M:SampleLibrary.Calculator.Add(System.Double,System.Double)">
            <summary>
            Adds two numbers together with overflow protection.
            </summary>
            <param name="a">The first number to add. Can be any valid double value.</param>
            <param name="b">The second number to add. Can be any valid double value.</param>
            <returns>The sum of a and b, or Double.PositiveInfinity/NegativeInfinity if overflow occurs.</returns>
            <exception cref="T:System.ArgumentException">Thrown when either parameter is NaN.</exception>
            <example>
            <code>
            var calculator = new Calculator();
            try
            {
                double result = calculator.Add(15.7, 24.3);
                Console.WriteLine($"15.7 + 24.3 = {result}");
            }
            catch (ArgumentException ex)
            {
                Console.WriteLine($"Invalid input: {ex.Message}");
            }
            </code>
            </example>
        </member>
        <member name="M:SampleLibrary.Calculator.Divide(System.Double,System.Double)">
            <summary>
            Divides one number by another with comprehensive error handling.
            </summary>
            <param name="dividend">The number to be divided. Must be a finite number.</param>
            <param name="divisor">The number to divide by. Cannot be zero or NaN.</param>
            <returns>The quotient of the division operation.</returns>
            <exception cref="T:System.DivideByZeroException">Thrown when divisor is exactly zero.</exception>
            <exception cref="T:System.ArgumentException">Thrown when either parameter is NaN or infinite.</exception>
            <example>
            Advanced division with error handling:
            <code>
            var calculator = new Calculator();
            try
            {
                double result = calculator.Divide(100, 3);
                Console.WriteLine($"100 ÷ 3 = {result:F6}");
                
                // Check for special cases
                if (double.IsInfinity(result))
                {
                    Console.WriteLine("Result is infinite");
                }
            }
            catch (DivideByZeroException)
            {
                Console.WriteLine("Cannot divide by zero");
            }
            catch (ArgumentException ex)
            {
                Console.WriteLine($"Invalid parameters: {ex.Message}");
            }
            </code>
            </example>
        </member>
        <member name="M:SampleLibrary.Calculator.CalculateCompoundInterest(System.Double,System.Double,System.Int32,System.Int32)">
            <summary>
            Calculates compound interest for financial applications.
            </summary>
            <param name="principal">The initial principal amount. Must be positive.</param>
            <param name="rate">The annual interest rate as a decimal (e.g., 0.05 for 5%).</param>
            <param name="timesCompounded">Number of times interest is compounded per year.</param>
            <param name="years">The number of years. Must be positive.</param>
            <returns>The final amount after compound interest.</returns>
            <exception cref="T:System.ArgumentOutOfRangeException">Thrown when any parameter is negative or zero.</exception>
            <exception cref="T:System.ArgumentException">Thrown when rate is greater than 1 (100%).</exception>
            <example>
            <code>
            var calculator = new Calculator();
            
            // Calculate compound interest for $1000 at 5% annual rate, 
            // compounded monthly for 10 years
            try
            {
                double finalAmount = calculator.CalculateCompoundInterest(1000, 0.05, 12, 10);
                double interest = finalAmount - 1000;
                
                Console.WriteLine($"Principal: $1,000.00");
                Console.WriteLine($"Final Amount: ${finalAmount:F2}");
                Console.WriteLine($"Interest Earned: ${interest:F2}");
            }
            catch (ArgumentOutOfRangeException ex)
            {
                Console.WriteLine($"Invalid range: {ex.Message}");
            }
            </code>
            </example>
        </member>
        <member name="P:SampleLibrary.Calculator.LastResult">
            <summary>
            Gets the result of the last calculation performed by this instance.
            </summary>
            <value>The last calculated result, or 0.0 if no operations have been performed.</value>
            <remarks>
            This property is thread-safe and will return the most recent calculation
            result for the current instance. Different Calculator instances maintain
            separate LastResult values.
            </remarks>
        </member>
        <member name="P:SampleLibrary.Calculator.OperationCount">
            <summary>
            Gets the total number of operations performed by this Calculator instance.
            </summary>
            <value>The count of arithmetic operations performed.</value>
        </member>
    </members>
</doc>
'@ | Set-Content -Path (Join-Path $dataDir "SampleLibrary.xml")

    # Sample 2: Enhanced Collection Extensions with Complex Examples
    @'
<?xml version="1.0"?>
<doc>
    <assembly>
        <name>CollectionExtensions</name>
    </assembly>
    <members>
        <member name="T:CollectionExtensions.ListExtensions">
            <summary>
            Provides high-performance extension methods for List&lt;T&gt; operations with null safety.
            </summary>
            <remarks>
            These extension methods provide safe alternatives to standard List&lt;T&gt; operations
            that may throw exceptions. All methods are optimized for performance and thread-safety.
            </remarks>
            <seealso cref="T:System.Collections.Generic.List`1"/>
        </member>
        <member name="M:CollectionExtensions.ListExtensions.SafeGet``1(System.Collections.Generic.List{``0},System.Int32)">
            <summary>
            Safely retrieves an element from a List&lt;T&gt; without throwing exceptions.
            </summary>
            <typeparam name="T">The type of elements in the list.</typeparam>
            <param name="list">The list to retrieve from. Cannot be null.</param>
            <param name="index">The zero-based index of the element to retrieve.</param>
            <returns>The element at the specified index, or default(T) if out of bounds or list is null.</returns>
            <exception cref="T:System.ArgumentNullException">Thrown when list is null.</exception>
            <example>
            Safe list access with null checking:
            <code>
            var numbers = new List&lt;int&gt; { 1, 2, 3, 4, 5 };
            
            // Safe access - won't throw exceptions
            int value1 = numbers.SafeGet(2);        // Returns 3
            int value2 = numbers.SafeGet(10);       // Returns 0 (default)
            int value3 = numbers.SafeGet(-1);       // Returns 0 (default)
            
            // Compare with unsafe access:
            try 
            {
                int unsafeValue = numbers[10];  // Would throw IndexOutOfRangeException
            }
            catch (IndexOutOfRangeException)
            {
                Console.WriteLine("Index out of range!");
            }
            </code>
            </example>
        </member>
        <member name="M:CollectionExtensions.ListExtensions.SafeRemove``1(System.Collections.Generic.List{``0},System.Int32)">
            <summary>
            Safely removes an element at the specified index without throwing exceptions.
            </summary>
            <typeparam name="T">The type of elements in the list.</typeparam>
            <param name="list">The list to remove from.</param>
            <param name="index">The zero-based index of the element to remove.</param>
            <returns>True if the element was successfully removed; otherwise, false.</returns>
            <exception cref="T:System.ArgumentNullException">Thrown when list is null.</exception>
            <example>
            <code>
            var items = new List&lt;string&gt; { "apple", "banana", "cherry" };
            
            bool removed1 = items.SafeRemove(1);    // Returns true, removes "banana"
            bool removed2 = items.SafeRemove(10);   // Returns false, index out of range
            
            Console.WriteLine($"Items remaining: {string.Join(", ", items)}");
            // Output: Items remaining: apple, cherry
            </code>
            </example>
        </member>
        <member name="T:CollectionExtensions.DictionaryHelper">
            <summary>
            Advanced helper methods for working with Dictionary&lt;TKey, TValue&gt; collections.
            </summary>
            <remarks>
            Provides thread-safe operations and advanced merging strategies for dictionaries.
            All methods handle edge cases and provide comprehensive error handling.
            </remarks>
            <seealso cref="T:System.Collections.Generic.Dictionary`2"/>
        </member>
        <member name="M:CollectionExtensions.DictionaryHelper.Merge``2(System.Collections.Generic.Dictionary{``0,``1},System.Collections.Generic.Dictionary{``0,``1})">
            <summary>
            Merges two dictionaries with conflict resolution for duplicate keys.
            </summary>
            <typeparam name="TKey">The type of keys in the dictionaries.</typeparam>
            <typeparam name="TValue">The type of values in the dictionaries.</typeparam>
            <param name="first">The first dictionary (takes precedence for duplicate keys).</param>
            <param name="second">The second dictionary to merge.</param>
            <returns>A new dictionary containing all key-value pairs from both dictionaries.</returns>
            <exception cref="T:System.ArgumentNullException">Thrown when either dictionary is null.</exception>
            <example>
            Dictionary merging with conflict resolution:
            <code>
            var settings1 = new Dictionary&lt;string, string&gt;
            {
                ["server"] = "localhost",
                ["port"] = "8080",
                ["timeout"] = "30"
            };
            
            var settings2 = new Dictionary&lt;string, string&gt;
            {
                ["port"] = "9090",      // Conflict - first dictionary wins
                ["database"] = "mydb",
                ["retries"] = "3"
            };
            
            try
            {
                var merged = DictionaryHelper.Merge(settings1, settings2);
                
                Console.WriteLine("Merged settings:");
                foreach (var kvp in merged)
                {
                    Console.WriteLine($"{kvp.Key}: {kvp.Value}");
                }
                // Output includes: port: 8080 (from first dictionary)
            }
            catch (ArgumentNullException ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
            </code>
            </example>
        </member>
        <member name="M:CollectionExtensions.DictionaryHelper.SafeTryGetValue``2(System.Collections.Generic.Dictionary{``0,``1},``0,``1@)">
            <summary>
            Thread-safe version of TryGetValue with additional null safety checks.
            </summary>
            <typeparam name="TKey">The type of keys in the dictionary.</typeparam>
            <typeparam name="TValue">The type of values in the dictionary.</typeparam>
            <param name="dictionary">The dictionary to search in.</param>
            <param name="key">The key to look for.</param>
            <param name="value">When this method returns, contains the value associated with the key if found.</param>
            <returns>True if the key was found; otherwise, false.</returns>
            <exception cref="T:System.ArgumentNullException">Thrown when dictionary is null.</exception>
            <example>
            <code>
            var cache = new Dictionary&lt;string, int&gt;
            {
                ["user123"] = 42,
                ["user456"] = 17
            };
            
            // Safe lookup with null checking
            if (DictionaryHelper.SafeTryGetValue(cache, "user123", out int score))
            {
                Console.WriteLine($"User score: {score}");
            }
            else
            {
                Console.WriteLine("User not found in cache");
            }
            </code>
            </example>
        </member>
    </members>
</doc>
'@ | Set-Content -Path (Join-Path $dataDir "CollectionExtensions.xml")

    # Sample 3: Interface hierarchy
    @'
<?xml version="1.0"?>
<doc>
    <assembly>
        <name>DataAccess</name>
    </assembly>
    <members>
        <member name="T:DataAccess.IRepository`1">
            <summary>
            Defines a generic repository pattern for data access.
            </summary>
            <typeparam name="T">The entity type.</typeparam>
        </member>
        <member name="M:DataAccess.IRepository`1.GetById(System.Int32)">
            <summary>
            Retrieves an entity by its identifier.
            </summary>
            <param name="id">The entity identifier.</param>
            <returns>The entity if found; otherwise, null.</returns>
        </member>
        <member name="T:DataAccess.IUserRepository">
            <summary>
            Repository for user entities.
            </summary>
            <seealso cref="T:DataAccess.IRepository`1"/>
        </member>
        <member name="M:DataAccess.IUserRepository.GetByEmail(System.String)">
            <summary>
            Retrieves a user by email address.
            </summary>
            <param name="email">The email address.</param>
            <returns>The user if found; otherwise, null.</returns>
        </member>
        <member name="T:DataAccess.UserRepository">
            <summary>
            Implementation of user repository using Entity Framework.
            </summary>
            <seealso cref="T:DataAccess.IUserRepository"/>
            <seealso cref="T:System.IDisposable"/>
        </member>
    </members>
</doc>
'@ | Set-Content -Path (Join-Path $dataDir "DataAccess.xml")

    Write-Host "Sample XML files created in: $dataDir" -ForegroundColor $colors.Success

    # Step 3: Index the documentation
    Write-Header "Indexing XML Documentation"
    
    Write-Section "Clean Index and Index All Files"
    Write-Example "apilens index $dataDir --clean --index $indexDir" "Clean previous index and index all XML files"
    Invoke-ApiLens @("index", $dataDir, "--clean", "--index", $indexDir)
    
    Write-Host "`nIndexing completed!" -ForegroundColor $colors.Success

    # Step 4: Basic Queries
    Write-Header "Basic Query Examples"
    
    Write-Section "Search by Name"
    Write-Example "apilens query Calculator" "Search for types/members with 'Calculator' in the name"
    Invoke-ApiLens @("query", "Calculator", "--index", $indexDir)
    
    Write-Example "apilens query Add" "Search for the Add method"
    Invoke-ApiLens @("query", "Add", "--index", $indexDir)
    
    Write-Section "Search by Content"
    Write-Example "apilens query 'extension methods' --type content" "Search in documentation content"
    Invoke-ApiLens @("query", "extension methods", "--type", "content", "--index", $indexDir)
    
    Write-Example "apilens query 'repository pattern' --type content" "Search for repository pattern mentions"
    Invoke-ApiLens @("query", "repository pattern", "--type", "content", "--index", $indexDir)

    # Step 5: Namespace Queries
    Write-Header "Namespace Exploration"
    
    Write-Section "List Members by Namespace"
    Write-Example "apilens query CollectionExtensions --type namespace" "Get all members in CollectionExtensions namespace"
    Invoke-ApiLens @("query", "CollectionExtensions", "--type", "namespace", "--index", $indexDir)
    
    Write-Example "apilens query DataAccess --type namespace" "Get all members in DataAccess namespace"
    Invoke-ApiLens @("query", "DataAccess", "--type", "namespace", "--index", $indexDir)

    # Step 6: Assembly Queries
    Write-Header "Assembly Exploration"
    
    Write-Section "List Members by Assembly"
    Write-Example "apilens query SampleLibrary --type assembly" "Get all members from SampleLibrary assembly"
    Invoke-ApiLens @("query", "SampleLibrary", "--type", "assembly", "--index", $indexDir)

    # Step 7: Exact ID Lookups
    Write-Header "Exact Member Lookups"
    
    Write-Section "Get Specific Members by ID"
    Write-Example "apilens query 'T:SampleLibrary.Calculator' --type id" "Get exact type information"
    Invoke-ApiLens @("query", "T:SampleLibrary.Calculator", "--type", "id", "--index", $indexDir)
    
    Write-Example "apilens query 'M:SampleLibrary.Calculator.Divide(System.Double,System.Double)' --type id" "Get specific method"
    Invoke-ApiLens @("query", "M:SampleLibrary.Calculator.Divide(System.Double,System.Double)", "--type", "id", "--index", $indexDir)

    # Step 8: Output Formats
    Write-Header "Output Format Examples"
    
    Write-Section "JSON Format (for LLMs/MCP)"
    Write-Example "apilens query Calculator --format json" "Get results in JSON format"
    Invoke-ApiLens @("query", "Calculator", "--format", "json", "--index", $indexDir)
    
    Write-Section "Markdown Format (for Documentation)"
    Write-Example "apilens query Calculator --format markdown" "Get results in Markdown format"
    Invoke-ApiLens @("query", "Calculator", "--format", "markdown", "--index", $indexDir)

    # Step 9: Advanced Scenarios
    Write-Header "Advanced Use Cases"
    
    Write-Section "Finding Generic Types"
    Write-Example "apilens query 'List<' --type content" "Search for List<T> usage"
    Invoke-ApiLens @("query", "List<", "--type", "content", "--index", $indexDir)
    
    Write-Section "Finding Exception References"
    Write-Example "apilens query 'DivideByZeroException' --type content" "Find methods that throw specific exceptions"
    Invoke-ApiLens @("query", "DivideByZeroException", "--type", "content", "--index", $indexDir)
    
    Write-Section "Interface Implementations"
    Write-Example "apilens query 'IRepository' --type content" "Find types related to IRepository"
    Invoke-ApiLens @("query", "IRepository", "--type", "content", "--index", $indexDir)

    # Step 10: MCP Integration Examples
    Write-Header "MCP Integration Scenarios"
    
    Write-Section "Machine-Readable Output"
    Write-Host "When integrated with MCP, ApiLens can provide structured data for LLMs:" -ForegroundColor $colors.Info
    
    Write-Example "apilens query 'GetById' --format json --max 5" "Limited results in JSON for LLM processing"
    $jsonOutput = Invoke-ApiLens @("query", "GetById", "--format", "json", "--max", "5", "--index", $indexDir)
    
    Write-Section "Chained Queries Example"
    Write-Host @"
Example LLM workflow:
1. User: "How do I work with repositories in this codebase?"
2. LLM queries: apilens query 'repository' --type content --format json
3. LLM discovers IRepository interface
4. LLM queries: apilens query 'T:DataAccess.IRepository``1' --type id --format json
5. LLM gets detailed information and can explain the pattern
"@ -ForegroundColor $colors.Info

    # Step 12: Specialized Query Commands
    Write-Header "Specialized Query Commands"
    
    Write-Section "Code Examples Command"
    Write-Host "Find methods with code examples to understand API usage patterns:" -ForegroundColor $colors.Info
    
    Write-Example "apilens examples" "List all methods with code examples"
    Invoke-ApiLens @("examples", "--index", $indexDir, "--max", "3")
    
    Write-Example "apilens examples 'Console.WriteLine'" "Search for specific patterns in code examples"
    Invoke-ApiLens @("examples", "Console.WriteLine", "--index", $indexDir)
    
    Write-Example "apilens examples --format json" "Get code examples in JSON format for LLM processing"
    $examplesJson = Invoke-ApiLens @("examples", "--index", $indexDir, "--format", "json", "--max", "2")
    
    Write-Section "Exceptions Command"
    Write-Host "Find methods that throw specific exceptions for error handling guidance:" -ForegroundColor $colors.Info
    
    Write-Example "apilens exceptions 'DivideByZeroException'" "Find methods that throw DivideByZeroException"
    Invoke-ApiLens @("exceptions", "DivideByZeroException", "--index", $indexDir)
    
    Write-Example "apilens exceptions 'ArgumentNullException' --details --format markdown" "Get detailed exception information"
    Invoke-ApiLens @("exceptions", "ArgumentNullException", "--details", "--format", "markdown", "--index", $indexDir)
    
    Write-Section "Complexity Command"
    Write-Host "Analyze method complexity to understand API difficulty:" -ForegroundColor $colors.Info
    
    Write-Example "apilens complexity --min-params 2" "Find methods with multiple parameters"
    Invoke-ApiLens @("complexity", "--min-params", "2", "--index", $indexDir)
    
    Write-Example "apilens complexity --stats --format json" "Get complexity statistics in JSON"
    $complexityJson = Invoke-ApiLens @("complexity", "--stats", "--format", "json", "--index", $indexDir, "--max", "5")

    # Step 13: Advanced Lucene Query Syntax
    Write-Header "Advanced Query Syntax"
    
    Write-Section "Wildcard Searches"
    Write-Host "Use * for multiple characters, ? for single character:" -ForegroundColor $colors.Info
    
    Write-Example "apilens query 'Calc*' --type content" "Find anything starting with 'Calc'"
    Invoke-ApiLens @("query", "Calc*", "--type", "content", "--index", $indexDir)
    
    Write-Example "apilens query 'Divid?' --type content" "Find words like 'Divide', 'Divider'"
    Invoke-ApiLens @("query", "Divid?", "--type", "content", "--index", $indexDir)
    
    Write-Section "Boolean Searches"
    Write-Host "Combine terms with AND, OR, NOT (must be uppercase):" -ForegroundColor $colors.Info
    
    Write-Example "apilens query 'mathematical AND operations' --type content" "Find docs with both terms"
    Invoke-ApiLens @("query", "mathematical AND operations", "--type", "content", "--index", $indexDir)
    
    Write-Example "apilens query 'add OR sum' --type content" "Find docs with either term"
    Invoke-ApiLens @("query", "add OR sum", "--type", "content", "--index", $indexDir)
    
    Write-Section "Fuzzy Searches"
    Write-Host "Use ~ for fuzzy matching to find similar terms:" -ForegroundColor $colors.Info
    
    Write-Example "apilens query 'calcuate~' --type content" "Find 'calculate' even with typo"
    Invoke-ApiLens @("query", "calcuate~", "--type", "content", "--index", $indexDir)

    # Step 14: JSON Processing Demonstration
    Write-Header "JSON Processing for LLMs"
    
    Write-Section "Parsing JSON Responses"
    Write-Host "Show how to parse JSON responses programmatically:" -ForegroundColor $colors.Info
    
    if ($examplesJson -and $examplesJson -ne "[]") {
        try {
            $parsedJson = $examplesJson | ConvertFrom-Json
            Write-Host "Parsed JSON structure:" -ForegroundColor $colors.Success
            foreach ($item in $parsedJson | Select-Object -First 1) {
                Write-Host "  Name: $($item.name)" -ForegroundColor $colors.Info
                Write-Host "  Type: $($item.memberType)" -ForegroundColor $colors.Info
                Write-Host "  Summary: $($item.summary)" -ForegroundColor $colors.Info
                if ($item.codeExamples -and $item.codeExamples.Count -gt 0) {
                    Write-Host "  Code Examples: $($item.codeExamples.Count)" -ForegroundColor $colors.Info
                }
            }
        }
        catch {
            Write-Host "JSON parsing example (no examples data available)" -ForegroundColor $colors.Info
        }
    }
    
    if ($complexityJson -and $complexityJson -ne "[]") {
        try {
            $parsedComplexity = $complexityJson | ConvertFrom-Json
            Write-Host "`nComplexity Analysis:" -ForegroundColor $colors.Success
            if ($parsedComplexity.statistics) {
                Write-Host "  Total Methods: $($parsedComplexity.statistics.totalMethods)" -ForegroundColor $colors.Info
                Write-Host "  Average Complexity: $($parsedComplexity.statistics.averageComplexity)" -ForegroundColor $colors.Info
                Write-Host "  Max Parameters: $($parsedComplexity.statistics.maxParameters)" -ForegroundColor $colors.Info
            }
        }
        catch {
            Write-Host "Complexity JSON parsing example (no data available)" -ForegroundColor $colors.Info
        }
    }

    # Step 15: Performance Testing
    Write-Header "Performance Demonstration"
    
    Write-Section "Indexing Large Documentation Sets"
    Write-Host "For production use, ApiLens can handle large documentation sets efficiently." -ForegroundColor $colors.Info
    Write-Host "Try indexing your .NET SDK documentation:" -ForegroundColor $colors.Info
    
    if ($IsWindows) {
        Write-Example "apilens index 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref' --pattern '**/*.xml'" "Index all .NET Core XML docs"
    } else {
        Write-Example "apilens index '/usr/share/dotnet/packs/Microsoft.NETCore.App.Ref' --pattern '**/*.xml'" "Index all .NET Core XML docs"
    }

    # Step 16: Cleanup
    Write-Header "Demo Complete!"
    
    Write-Host @"
Key Takeaways:
- ApiLens provides fast, full-text search of .NET API documentation
- Specialized commands (examples, exceptions, complexity) enable targeted analysis
- Advanced Lucene query syntax supports wildcards, fuzzy search, and boolean operators
- Multiple query types and output formats support different use cases
- JSON output enables MCP/LLM integration with structured data
- Rich metadata extraction provides code examples and exception details
- Cross-references help discover related types and understand APIs

Next Steps:
1. Index your project's XML documentation
2. Integrate with MCP for LLM-powered API exploration
3. Use specialized commands to understand API patterns and complexity
4. Use in CI/CD for documentation validation
5. Create custom tooling using the JSON output
"@ -ForegroundColor $colors.Success

    Write-Section "Cleanup"
    Write-Host "Would you like to remove the demo files?" -ForegroundColor $colors.Info
    Write-Host "  - Demo index: $indexDir" -ForegroundColor $colors.Info
    Write-Host "  - Sample data: $dataDir" -ForegroundColor $colors.Info
    Write-Host "`nRemove demo files? (y/N): " -NoNewline -ForegroundColor $colors.Warning
    $cleanup = Read-Host
    if ($cleanup -eq 'y') {
        Write-Host "`nCleaning up..." -ForegroundColor $colors.Info
        
        # Clean up index directory
        if (Test-Path $indexDir) {
            try {
                Remove-Item -Path $indexDir -Recurse -Force -ErrorAction Stop
                Write-Host "✓ Demo index removed: $indexDir" -ForegroundColor $colors.Success
            }
            catch {
                Write-Host "⚠ Could not remove index directory: $_" -ForegroundColor $colors.Warning
            }
        }
        
        # Clean up data directory
        if (Test-Path $dataDir) {
            try {
                Remove-Item -Path $dataDir -Recurse -Force -ErrorAction Stop
                Write-Host "✓ Sample data removed: $dataDir" -ForegroundColor $colors.Success
            }
            catch {
                Write-Host "⚠ Could not remove data directory: $_" -ForegroundColor $colors.Warning
            }
        }
        
        Write-Host "`nCleanup complete!" -ForegroundColor $colors.Success
    }
    else {
        Write-Host "`nDemo files retained for further exploration." -ForegroundColor $colors.Info
    }
}
catch {
    Write-Host "Error: $_" -ForegroundColor $colors.Error
    exit 1
}
finally {
    Pop-Location
}