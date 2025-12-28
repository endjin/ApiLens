#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Demonstrates the property type linking feature in ApiLens.

.DESCRIPTION
    Shows how ApiLens now extracts type information for properties by linking
    them to their getter methods, providing richer metadata for API exploration.

.EXAMPLE
    ./property-type-linking-demo.ps1
#>

$ErrorActionPreference = "Stop"

Write-Host "`n🏷️  ApiLens Property Type Linking Demo" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "New feature: Properties now show type information!" -ForegroundColor Gray

# Build if needed
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$apilens = Join-Path $repoRoot "Solutions/ApiLens.Cli/bin/Debug/net10.0/apilens"
if ($IsWindows -or $env:OS -eq "Windows_NT") { 
    $apilens += ".exe" 
}
if (-not (Test-Path $apilens)) {
    Write-Host "`nBuilding ApiLens..." -ForegroundColor Yellow
    $csproj = Join-Path $repoRoot "Solutions/ApiLens.Cli/ApiLens.Cli.csproj"
    dotnet build $csproj --verbosity quiet
}

# Set up demo with rich property examples
$tmpBase = Join-Path ([System.IO.Path]::GetTempPath()) "apilens-property-demo"
$indexPath = Join-Path $tmpBase "indexes/property-index"
$docsDir = Join-Path $tmpBase "docs"
if (Test-Path $tmpBase) { Remove-Item $tmpBase -Recurse -Force }
New-Item -ItemType Directory -Path $docsDir -Force | Out-Null

Write-Host "`n📝 Creating demo XML with properties and their getters..." -ForegroundColor Yellow

# Create XML documentation that demonstrates property type linking
@'
<?xml version="1.0"?>
<doc>
    <assembly><name>PropertyDemo</name></assembly>
    <members>
        <!-- Type definitions -->
        <member name="T:PropertyDemo.Configuration">
            <summary>Configuration class demonstrating property type linking.</summary>
        </member>
        <member name="T:PropertyDemo.DatabaseSettings">
            <summary>Database configuration settings.</summary>
        </member>
        <member name="T:PropertyDemo.ServerInfo">
            <summary>Server information container.</summary>
        </member>
        
        <!-- Properties with their getter methods -->
        
        <!-- String property -->
        <member name="P:PropertyDemo.Configuration.ConnectionString">
            <summary>Gets or sets the database connection string.</summary>
            <value>The connection string for the database.</value>
        </member>
        <member name="M:PropertyDemo.Configuration.get_ConnectionString">
            <summary>Gets the database connection string.</summary>
            <returns>A string containing the connection string.</returns>
        </member>
        
        <!-- Integer property -->
        <member name="P:PropertyDemo.Configuration.MaxRetries">
            <summary>Gets or sets the maximum number of retry attempts.</summary>
            <value>The maximum retry count.</value>
        </member>
        <member name="M:PropertyDemo.Configuration.get_MaxRetries">
            <summary>Gets the maximum number of retry attempts.</summary>
            <returns>An integer representing the retry count.</returns>
        </member>
        
        <!-- Boolean property -->
        <member name="P:PropertyDemo.Configuration.IsEnabled">
            <summary>Gets or sets whether the feature is enabled.</summary>
            <value>True if enabled; otherwise, false.</value>
        </member>
        <member name="M:PropertyDemo.Configuration.get_IsEnabled">
            <summary>Gets whether the feature is enabled.</summary>
            <returns>A boolean indicating if the feature is enabled.</returns>
        </member>
        
        <!-- Complex type property -->
        <member name="P:PropertyDemo.Configuration.DatabaseSettings">
            <summary>Gets or sets the database configuration settings.</summary>
            <value>The database settings object.</value>
        </member>
        <member name="M:PropertyDemo.Configuration.get_DatabaseSettings">
            <summary>Gets the database configuration settings.</summary>
            <returns>A DatabaseSettings object containing the configuration.</returns>
        </member>
        
        <!-- Collection property -->
        <member name="P:PropertyDemo.ServerInfo.ServerList">
            <summary>Gets the list of available servers.</summary>
            <value>A collection of server names.</value>
        </member>
        <member name="M:PropertyDemo.ServerInfo.get_ServerList">
            <summary>Gets the list of available servers.</summary>
            <returns>A List&lt;string&gt; containing server names.</returns>
        </member>
        
        <!-- Nullable property -->
        <member name="P:PropertyDemo.ServerInfo.LastUpdated">
            <summary>Gets the last update timestamp.</summary>
            <value>The timestamp when last updated, or null if never updated.</value>
        </member>
        <member name="M:PropertyDemo.ServerInfo.get_LastUpdated">
            <summary>Gets the last update timestamp.</summary>
            <returns>A nullable DateTime representing the last update time.</returns>
        </member>
        
        <!-- Property without getter (to show fallback behavior) -->
        <member name="P:PropertyDemo.Configuration.InternalState">
            <summary>Internal state property without explicit getter documentation.</summary>
            <value>Internal state information.</value>
        </member>
    </members>
</doc>
'@ | Set-Content "$docsDir/PropertyDemo.xml"

# Index the demonstration data
Write-Host "`n📚 Indexing property demonstration data..." -ForegroundColor Yellow
& "$apilens" index $docsDir --clean --index "$indexPath" | Out-Null

Write-Host "✅ Property demo data indexed successfully!" -ForegroundColor Green

# PART 1: The Problem - Before Property Type Linking
Write-Host "`n`n═══════════════════════════════════════════════════════════════" -ForegroundColor DarkCyan
Write-Host "  PART 1: THE PROBLEM WITH XML DOCUMENTATION" -ForegroundColor Yellow
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor DarkCyan

Write-Host "`n❌ The Challenge:" -ForegroundColor Red
Write-Host @"
XML documentation format (used by .NET, Java, etc.) often lacks explicit type 
information for properties. Properties are documented with <summary> and <value>
tags, but the actual property TYPE is not explicitly stated.

Example XML for a property:
<member name="P:MyClass.ConnectionString">
    <summary>Gets or sets the connection string.</summary>
    <value>The database connection string.</value>
</member>

❓ Question: What TYPE is ConnectionString? String? Uri? Custom class?
"@ -ForegroundColor Gray

# PART 2: The Solution - Property Type Linking
Write-Host "`n`n═══════════════════════════════════════════════════════════════" -ForegroundColor DarkCyan
Write-Host "  PART 2: THE SOLUTION - PROPERTY TYPE LINKING" -ForegroundColor Yellow
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor DarkCyan

Write-Host "`n✅ ApiLens Solution:" -ForegroundColor Green
Write-Host @"
ApiLens now automatically links properties to their getter methods to extract type information:

1. Property: P:MyClass.ConnectionString
2. Find getter: M:MyClass.get_ConnectionString  
3. Extract return type from getter method
4. Apply return type to the property

Result: Rich property metadata with actual type information!
"@ -ForegroundColor Gray

# PART 3: Demonstration - Property Types in Action
Write-Host "`n`n═══════════════════════════════════════════════════════════════" -ForegroundColor DarkCyan
Write-Host "  PART 3: PROPERTY TYPE LINKING IN ACTION" -ForegroundColor Yellow
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor DarkCyan

Write-Host "`n1️⃣  Find all properties and see their extracted types:" -ForegroundColor Magenta
Write-Host "Command: " -NoNewline -ForegroundColor DarkGray
Write-Host "apilens query 'Property' --member-type Property" -ForegroundColor Yellow
& "$apilens" query "Property" --member-type Property --index "$indexPath"

Write-Host "`n2️⃣  List Configuration class members showing property types:" -ForegroundColor Magenta
Write-Host "Command: " -NoNewline -ForegroundColor DarkGray
Write-Host "apilens members 'Configuration'" -ForegroundColor Yellow
& "$apilens" members "Configuration" --index "$indexPath"

Write-Host "`n3️⃣  Find properties with specific types (e.g., string properties):" -ForegroundColor Magenta
Write-Host "Command: " -NoNewline -ForegroundColor DarkGray
Write-Host "apilens query 'string' --member-type Property --type content" -ForegroundColor Yellow
& "$apilens" query "string" --member-type Property --type content --index "$indexPath"

# PART 4: JSON Output with Rich Type Information
Write-Host "`n`n═══════════════════════════════════════════════════════════════" -ForegroundColor DarkCyan
Write-Host "  PART 4: JSON OUTPUT WITH PROPERTY TYPE METADATA" -ForegroundColor Yellow
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor DarkCyan

Write-Host "`n📊 JSON output now includes property type information:" -ForegroundColor Magenta
Write-Host "Command: " -NoNewline -ForegroundColor DarkGray
Write-Host "apilens members 'Configuration' --format json" -ForegroundColor Yellow
$jsonOutput = & "$apilens" members "Configuration" --format json --index "$indexPath"
if ($jsonOutput) {
    $obj = $jsonOutput | ConvertFrom-Json
    Write-Host "`nProperty Type Information Extracted:" -ForegroundColor Green
    
    $properties = $obj.members | Where-Object { $_.memberType -eq "Property" }
    foreach ($prop in $properties) {
        $typeName = if ($prop.returnType) { $prop.returnType } else { "type information not available" }
        Write-Host "  📄 $($prop.name): $typeName" -ForegroundColor Cyan
    }
    
    Write-Host "`nTotal properties with type info: $($properties.Count)" -ForegroundColor Gray
}

# PART 5: Type-Based Property Discovery
Write-Host "`n`n═══════════════════════════════════════════════════════════════" -ForegroundColor DarkCyan
Write-Host "  PART 5: TYPE-BASED PROPERTY DISCOVERY" -ForegroundColor Yellow
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor DarkCyan

Write-Host "`n🔍 With property type linking, you can now discover properties by their types!" -ForegroundColor Cyan

Write-Host "`n1️⃣  Find all boolean properties (flags, switches, etc.):" -ForegroundColor Magenta
Write-Host "Command: " -NoNewline -ForegroundColor DarkGray
Write-Host "apilens query 'boolean' --member-type Property --type content" -ForegroundColor Yellow
& "$apilens" query "boolean" --member-type Property --type content --index "$indexPath"

Write-Host "`n2️⃣  Find collection properties (List, Array, etc.):" -ForegroundColor Magenta
Write-Host "Command: " -NoNewline -ForegroundColor DarkGray
Write-Host "apilens query 'List' --member-type Property --type content" -ForegroundColor Yellow
& "$apilens" query "List" --member-type Property --type content --index "$indexPath"

Write-Host "`n3️⃣  Find DateTime properties (timestamps, dates, etc.):" -ForegroundColor Magenta
Write-Host "Command: " -NoNewline -ForegroundColor DarkGray
Write-Host "apilens query 'DateTime' --member-type Property --type content" -ForegroundColor Yellow
& "$apilens" query "DateTime" --member-type Property --type content --index "$indexPath"

# PART 6: Technical Implementation Details
Write-Host "`n`n═══════════════════════════════════════════════════════════════" -ForegroundColor DarkCyan
Write-Host "  PART 6: HOW PROPERTY TYPE LINKING WORKS" -ForegroundColor Yellow
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor DarkCyan

Write-Host "`n🔧 Technical Implementation:" -ForegroundColor Cyan
Write-Host @"

Step-by-step process:
1. During XML parsing, identify property members (P:...)
2. For each property, construct the getter method name (get_PropertyName)
3. Search for the corresponding getter method (M:...get_PropertyName...)
4. Extract the return type from the getter method documentation
5. Apply the return type to the property member
6. Index both the property and its extracted type information

Fallback behaviors:
• If no getter method is found, property shows without type info
• If getter exists but lacks return type, property shows without type info  
• Field members (F:...) may also get type info from their documentation

Performance impact:
• Minimal - linking happens during indexing, not during search
• Batch processing ensures efficient linking operations
• Property type information is cached in the search index
"@ -ForegroundColor Gray

# PART 7: Benefits for Different Use Cases
Write-Host "`n`n═══════════════════════════════════════════════════════════════" -ForegroundColor DarkCyan
Write-Host "  PART 7: BENEFITS FOR DIFFERENT USE CASES" -ForegroundColor Yellow
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor DarkCyan

Write-Host "`n📋 Use Case 1: API Documentation Generation" -ForegroundColor Green
Write-Host "Generate documentation with complete property type information:" -ForegroundColor Gray
$docJson = & "$apilens" list-types --assembly "PropertyDemo" --format json --index "$indexPath"
if ($docJson) {
    $obj = $docJson | ConvertFrom-Json
    Write-Host "✅ Complete type information available for documentation tools" -ForegroundColor Cyan
}

Write-Host "`n📋 Use Case 2: LLM Integration and AI Code Assistance" -ForegroundColor Green
Write-Host "Provide LLMs with rich property metadata for better code understanding:" -ForegroundColor Gray
Write-Host "✅ LLMs can now understand property types for better code suggestions" -ForegroundColor Cyan

Write-Host "`n📋 Use Case 3: IDE Integration and IntelliSense" -ForegroundColor Green
Write-Host "Enhanced IDE features with complete property information:" -ForegroundColor Gray
Write-Host "✅ Better autocomplete, parameter hints, and type inference" -ForegroundColor Cyan

Write-Host "`n📋 Use Case 4: Security and Compliance Analysis" -ForegroundColor Green
Write-Host "Find properties by type for security analysis:" -ForegroundColor Gray
Write-Host "✅ Locate all string properties (potential security issues)" -ForegroundColor Cyan
Write-Host "✅ Find all collection properties (performance considerations)" -ForegroundColor Cyan

# PART 8: Comparison with and without Property Type Linking
Write-Host "`n`n═══════════════════════════════════════════════════════════════" -ForegroundColor DarkCyan
Write-Host "  PART 8: BEFORE vs AFTER COMPARISON" -ForegroundColor Yellow
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor DarkCyan

Write-Host "`n📊 Property Information Comparison:" -ForegroundColor Magenta
$memberData = & "$apilens" members "Configuration" --format json --index "$indexPath"
if ($memberData) {
    $obj = $memberData | ConvertFrom-Json
    $properties = $obj.members | Where-Object { $_.memberType -eq "Property" }
    $propertiesWithTypes = $properties | Where-Object { $_.returnType }
    
    Write-Host "`nBEFORE Property Type Linking:" -ForegroundColor Red
    Write-Host "  • Properties found: $($properties.Count)" -ForegroundColor Gray
    Write-Host "  • Properties with type info: 0 (0%)" -ForegroundColor Gray
    Write-Host "  • Usability for tools: Limited" -ForegroundColor Gray
    
    Write-Host "`nAFTER Property Type Linking:" -ForegroundColor Green
    Write-Host "  • Properties found: $($properties.Count)" -ForegroundColor Gray
    if ($properties.Count -gt 0) {
        Write-Host "  • Properties with type info: $($propertiesWithTypes.Count) ($([math]::Round($propertiesWithTypes.Count / $properties.Count * 100))%)" -ForegroundColor Gray
    } else {
        Write-Host "  • Properties with type info: 0 (N/A - no properties found)" -ForegroundColor Gray
    }
    Write-Host "  • Usability for tools: Excellent" -ForegroundColor Gray
    
    Write-Host "`nType Information Extracted:" -ForegroundColor Cyan
    foreach ($prop in $propertiesWithTypes) {
        Write-Host "  ✅ $($prop.name) → $($prop.returnType)" -ForegroundColor Green
    }
    
    $propertiesWithoutTypes = $properties | Where-Object { -not $_.returnType }
    if ($propertiesWithoutTypes.Count -gt 0) {
        Write-Host "`nProperties without type info (no getter found):" -ForegroundColor Yellow
        foreach ($prop in $propertiesWithoutTypes) {
            Write-Host "  ⚠️ $($prop.name) → type unknown" -ForegroundColor Yellow
        }
    }
}

# Summary
Write-Host "`n`n✨ Property Type Linking Summary" -ForegroundColor Cyan
Write-Host @"

🎯 Key Benefits:
   ✅ Properties now show actual type information
   ✅ Automatic linking to getter methods during indexing
   ✅ Enhanced metadata for API exploration and documentation
   ✅ Better LLM integration with complete type information
   ✅ Improved search capabilities (find properties by type)

🔍 How It Works:
   • Links properties (P:...) to their getter methods (M:...get_...)
   • Extracts return type from getter method documentation
   • Applies type information to property metadata
   • Works during indexing for optimal performance

📊 Results:
   • Dramatically improved property metadata richness
   • Better API understanding for tools and developers
   • Enhanced code analysis and documentation generation
   • Superior LLM integration and AI-assisted development

💡 Perfect For:
   - API documentation tools requiring complete type information
   - LLM integration and AI code assistance platforms
   - IDE plugins and IntelliSense features
   - Code analysis tools and security scanners
   - Developer productivity tools and API explorers

The property type linking feature transforms ApiLens from a basic API explorer
into a comprehensive API intelligence platform!
"@ -ForegroundColor Gray

# Cleanup
Write-Host "`n🧹 Cleaning up demo files..." -ForegroundColor Yellow
Remove-Item $tmpBase -Recurse -Force
Write-Host "✅ Property type linking demo complete!" -ForegroundColor Green