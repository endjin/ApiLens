#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Demonstrates the ApiLens hierarchy command for exploring type relationships.

.DESCRIPTION
    Shows how to use the hierarchy command to explore:
    - Type inheritance chains
    - Interface implementations
    - Derived types
    - Type members with inheritance
#>

param(
    [string]$IndexPath = ""
)

# Set default index path if not provided
if (-not $IndexPath) {
    $tmpBase = Join-Path ([System.IO.Path]::GetTempPath()) "apilens-demo"
    $IndexPath = Join-Path $tmpBase "indexes/hierarchy-demo-index"
}

Write-Host "`n🔍 ApiLens Type Hierarchy Demo" -ForegroundColor Cyan
Write-Host "==============================" -ForegroundColor Cyan
Write-Host "Exploring type relationships and inheritance hierarchies`n" -ForegroundColor Gray

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

# Create sample documentation with type hierarchies
Write-Host "📚 Creating sample documentation with type hierarchies..." -ForegroundColor Yellow
$tmpBase = Join-Path ([System.IO.Path]::GetTempPath()) "apilens-demo"
$docsDir = Join-Path $tmpBase "docs/hierarchy-docs"
New-Item -ItemType Directory -Path $docsDir -Force | Out-Null

# Sample: Complex inheritance hierarchy
@'
<?xml version="1.0"?>
<doc>
    <assembly><name>HierarchyDemo</name></assembly>
    <members>
        <!-- Base Interface -->
        <member name="T:HierarchyDemo.IEntity">
            <summary>Base interface for all entities in the system.</summary>
        </member>
        <member name="P:HierarchyDemo.IEntity.Id">
            <summary>Gets or sets the unique identifier.</summary>
        </member>
        
        <!-- Extended Interface -->
        <member name="T:HierarchyDemo.IAuditable">
            <summary>Interface for entities that support auditing.</summary>
            <seealso cref="T:HierarchyDemo.IEntity"/>
        </member>
        <member name="P:HierarchyDemo.IAuditable.CreatedAt">
            <summary>Gets the creation timestamp.</summary>
        </member>
        <member name="P:HierarchyDemo.IAuditable.ModifiedAt">
            <summary>Gets the last modification timestamp.</summary>
        </member>
        
        <!-- Base Class -->
        <member name="T:HierarchyDemo.EntityBase">
            <summary>
            Abstract base class for all entities.
            Implements the core entity interface.
            </summary>
            <seealso cref="T:HierarchyDemo.IEntity"/>
        </member>
        <member name="M:HierarchyDemo.EntityBase.#ctor">
            <summary>Initializes a new instance of the EntityBase class.</summary>
        </member>
        <member name="M:HierarchyDemo.EntityBase.Validate">
            <summary>Validates the entity state.</summary>
            <returns>True if valid; otherwise, false.</returns>
        </member>
        
        <!-- Derived Class -->
        <member name="T:HierarchyDemo.AuditableEntity">
            <summary>
            Base class for entities that support auditing.
            Extends EntityBase with audit functionality.
            </summary>
            <seealso cref="T:HierarchyDemo.EntityBase"/>
            <seealso cref="T:HierarchyDemo.IAuditable"/>
        </member>
        <member name="M:HierarchyDemo.AuditableEntity.UpdateTimestamp">
            <summary>Updates the modification timestamp to the current time.</summary>
        </member>
        
        <!-- Concrete Implementation -->
        <member name="T:HierarchyDemo.User">
            <summary>
            Represents a user in the system.
            Inherits from AuditableEntity and implements additional interfaces.
            </summary>
            <seealso cref="T:HierarchyDemo.AuditableEntity"/>
            <seealso cref="T:System.IComparable`1"/>
            <seealso cref="T:System.IEquatable`1"/>
        </member>
        <member name="P:HierarchyDemo.User.Username">
            <summary>Gets or sets the username.</summary>
        </member>
        <member name="P:HierarchyDemo.User.Email">
            <summary>Gets or sets the email address.</summary>
        </member>
        <member name="M:HierarchyDemo.User.CompareTo(HierarchyDemo.User)">
            <summary>Compares this user to another user by username.</summary>
        </member>
        
        <!-- Another Concrete Implementation -->
        <member name="T:HierarchyDemo.Product">
            <summary>
            Represents a product in the system.
            Another implementation of AuditableEntity.
            </summary>
            <seealso cref="T:HierarchyDemo.AuditableEntity"/>
        </member>
        <member name="P:HierarchyDemo.Product.Name">
            <summary>Gets or sets the product name.</summary>
        </member>
        <member name="P:HierarchyDemo.Product.Price">
            <summary>Gets or sets the product price.</summary>
        </member>
        
        <!-- Generic Repository Pattern -->
        <member name="T:HierarchyDemo.IRepository`1">
            <summary>
            Generic repository interface for data access.
            </summary>
            <typeparam name="T">The entity type.</typeparam>
        </member>
        <member name="M:HierarchyDemo.IRepository`1.GetById(`0)">
            <summary>Gets an entity by its identifier.</summary>
        </member>
        <member name="M:HierarchyDemo.IRepository`1.Add(`0)">
            <summary>Adds a new entity.</summary>
        </member>
        
        <!-- Repository Implementation -->
        <member name="T:HierarchyDemo.Repository`1">
            <summary>
            Generic repository implementation.
            </summary>
            <typeparam name="T">The entity type.</typeparam>
            <seealso cref="T:HierarchyDemo.IRepository`1"/>
        </member>
        
        <!-- Specialized Repository -->
        <member name="T:HierarchyDemo.UserRepository">
            <summary>
            Specialized repository for User entities.
            </summary>
            <seealso cref="T:HierarchyDemo.Repository`1"/>
            <seealso cref="T:HierarchyDemo.IUserRepository"/>
        </member>
        <member name="M:HierarchyDemo.UserRepository.FindByEmail(System.String)">
            <summary>Finds a user by email address.</summary>
        </member>
    </members>
</doc>
'@ | Set-Content "$docsDir/HierarchyDemo.xml"

# Index the documentation
Write-Host "`nIndexing documentation..." -ForegroundColor Yellow
Write-Host "Command: " -NoNewline -ForegroundColor DarkGray
Write-Host "apilens index $docsDir --clean --index $IndexPath" -ForegroundColor Yellow
& $apilens index $docsDir --clean --index $IndexPath | Out-Null
Write-Host "✅ Documentation indexed successfully!" -ForegroundColor Green

# Demo 1: Explore Type Hierarchy
Write-Host "`n`n🎯 DEMO 1: Exploring Type Hierarchies" -ForegroundColor Yellow
Write-Host "Finding and analyzing type inheritance relationships" -ForegroundColor Gray

Write-Host "`n1.1 Explore the User class hierarchy:" -ForegroundColor Magenta
Write-Host "Command: " -NoNewline -ForegroundColor DarkGray
Write-Host "apilens hierarchy 'User' --index $IndexPath" -ForegroundColor Yellow
& $apilens hierarchy "User" --index $IndexPath

Write-Host "`n1.2 Explore base class EntityBase with derived types:" -ForegroundColor Magenta
Write-Host "Command: " -NoNewline -ForegroundColor DarkGray
Write-Host "apilens hierarchy 'EntityBase' --index $IndexPath" -ForegroundColor Yellow
& $apilens hierarchy "EntityBase" --index $IndexPath

Write-Host "`n1.3 Explore interface IEntity and its implementations:" -ForegroundColor Magenta
Write-Host "Command: " -NoNewline -ForegroundColor DarkGray
Write-Host "apilens hierarchy 'IEntity' --index $IndexPath" -ForegroundColor Yellow
& $apilens hierarchy "IEntity" --index $IndexPath

# Demo 2: Show Members with Inheritance
Write-Host "`n`n🎯 DEMO 2: Type Members and Inheritance" -ForegroundColor Yellow
Write-Host "Viewing type members including inherited ones" -ForegroundColor Gray

Write-Host "`n2.1 Show members of AuditableEntity:" -ForegroundColor Magenta
Write-Host "Command: " -NoNewline -ForegroundColor DarkGray
Write-Host "apilens hierarchy 'AuditableEntity' --show-members --index $IndexPath" -ForegroundColor Yellow
& $apilens hierarchy "AuditableEntity" --show-members --index $IndexPath

Write-Host "`n2.2 Show all members including inherited:" -ForegroundColor Magenta
Write-Host "Command: " -NoNewline -ForegroundColor DarkGray
Write-Host "apilens hierarchy 'User' --show-members --show-inherited --index $IndexPath" -ForegroundColor Yellow
& $apilens hierarchy "User" --show-members --show-inherited --index $IndexPath

# Demo 3: Generic Types
Write-Host "`n`n🎯 DEMO 3: Generic Type Hierarchies" -ForegroundColor Yellow
Write-Host "Working with generic types and their relationships" -ForegroundColor Gray

Write-Host "`n3.1 Explore generic interface IRepository<T>:" -ForegroundColor Magenta
Write-Host "Command: " -NoNewline -ForegroundColor DarkGray
Write-Host "apilens hierarchy 'IRepository' --index $IndexPath" -ForegroundColor Yellow
& $apilens hierarchy "IRepository" --index $IndexPath

Write-Host "`n3.2 Explore generic implementation Repository<T>:" -ForegroundColor Magenta
Write-Host "Command: " -NoNewline -ForegroundColor DarkGray
Write-Host "apilens hierarchy 'Repository' --index $IndexPath" -ForegroundColor Yellow
& $apilens hierarchy "Repository" --index $IndexPath

# Demo 4: JSON Output for Automation
Write-Host "`n`n🎯 DEMO 4: JSON Output for Automation" -ForegroundColor Yellow
Write-Host "Getting hierarchy information in JSON format for tool integration" -ForegroundColor Gray

Write-Host "`n4.1 Get hierarchy in JSON format:" -ForegroundColor Magenta
Write-Host "Command: " -NoNewline -ForegroundColor DarkGray
Write-Host "apilens hierarchy 'User' --format json --index $IndexPath" -ForegroundColor Yellow
$jsonHierarchy = & $apilens hierarchy "User" --format json --index $IndexPath
Write-Host $jsonHierarchy -ForegroundColor Cyan

Write-Host "`n4.2 Parse JSON for automation:" -ForegroundColor Magenta
if ($jsonHierarchy) {
    try {
        $hierarchy = $jsonHierarchy | ConvertFrom-Json
        Write-Host "Type Name: $($hierarchy.type.name)" -ForegroundColor Green
        Write-Host "Full Name: $($hierarchy.type.fullName)" -ForegroundColor Green
        Write-Host "Base Types Count: $($hierarchy.baseTypes.Count)" -ForegroundColor Green
        Write-Host "Interfaces Count: $($hierarchy.interfaces.Count)" -ForegroundColor Green
        Write-Host "Derived Types Count: $($hierarchy.derivedTypes.Count)" -ForegroundColor Green
        
        if ($hierarchy.baseTypes.Count -gt 0) {
            Write-Host "`nBase Types:" -ForegroundColor Yellow
            foreach ($base in $hierarchy.baseTypes) {
                Write-Host "  - $($base.name)" -ForegroundColor Gray
            }
        }
        
        if ($hierarchy.interfaces.Count -gt 0) {
            Write-Host "`nImplemented Interfaces:" -ForegroundColor Yellow
            foreach ($interface in $hierarchy.interfaces) {
                Write-Host "  - $($interface.name)" -ForegroundColor Gray
            }
        }
    }
    catch {
        Write-Host "Could not parse JSON response" -ForegroundColor Gray
    }
}

# Demo 5: Markdown Output for Documentation
Write-Host "`n`n🎯 DEMO 5: Markdown Output for Documentation" -ForegroundColor Yellow
Write-Host "Generating documentation-ready hierarchy information" -ForegroundColor Gray

Write-Host "`n5.1 Generate Markdown documentation:" -ForegroundColor Magenta
Write-Host "Command: " -NoNewline -ForegroundColor DarkGray
Write-Host "apilens hierarchy 'AuditableEntity' --format markdown --show-members --index $IndexPath" -ForegroundColor Yellow
& $apilens hierarchy "AuditableEntity" --format markdown --show-members --index $IndexPath

# Demo 6: Practical Use Cases
Write-Host "`n`n🎯 DEMO 6: Practical Use Cases" -ForegroundColor Yellow
Write-Host "Real-world scenarios for using the hierarchy command" -ForegroundColor Gray

Write-Host "`n📋 Use Case 1: Understanding Implementation Details" -ForegroundColor Green
Write-Host "When you need to understand what interfaces a class implements:" -ForegroundColor Gray
& $apilens hierarchy "UserRepository" --index $IndexPath

Write-Host "`n📋 Use Case 2: Finding All Implementations" -ForegroundColor Green
Write-Host "When you need to find all classes that implement an interface:" -ForegroundColor Gray
& $apilens hierarchy "IAuditable" --index $IndexPath

Write-Host "`n📋 Use Case 3: Analyzing Inheritance Chain" -ForegroundColor Green
Write-Host "When you need to understand the full inheritance hierarchy:" -ForegroundColor Gray
& $apilens hierarchy "Product" --show-members --index $IndexPath

# Summary
Write-Host "`n`n✨ Summary of Hierarchy Command Features" -ForegroundColor Cyan
Write-Host @"

🎯 Key Capabilities:
   • Explore type inheritance chains
   • Find interface implementations
   • Discover derived types
   • View type members with inheritance
   • Handle generic types correctly

📊 Output Formats:
   • Table: Human-readable console output (default)
   • JSON: Structured data for automation and tool integration
   • Markdown: Documentation-ready formatted output

🔍 Command Options:
   • --show-members: Include type members in output
   • --show-inherited: Show inherited members (requires --show-members)
   • --format: Choose output format (table|json|markdown)
   • --max: Limit number of results

💡 Use Cases:
   • Understanding complex inheritance hierarchies
   • Finding all implementations of an interface
   • Discovering what interfaces a type implements
   • Analyzing base class relationships
   • Generating type documentation
   • API exploration and discovery

🤖 Integration:
   • JSON output enables integration with development tools
   • Perfect for LLMs to understand type relationships
   • Supports automated documentation generation
   • Helps with refactoring and code analysis

The hierarchy command provides deep insights into type relationships,
making it easier to understand and navigate complex codebases.
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