# ApiLens

ApiLens is a .NET 9 CLI application that indexes and queries .NET XML API documentation using Lucene.NET. It's designed to make .NET API documentation searchable and accessible, particularly for LLMs through the Model Context Protocol (MCP).

## Features

- **Index XML Documentation**: Parse and index .NET XML documentation files with incremental updates
- **Project/Solution Analysis**: Analyze .NET projects and solutions to discover and index dependencies
- **NuGet Package Support**: Automatically discover and index documentation from NuGet cache
- **Package Exploration**: Interactive exploration of packages with guided navigation
- **Type Hierarchy Discovery**: Explore inheritance chains and interface implementations
- **Version Tracking**: Track and display package versions and target frameworks
- **Full-Text Search**: Query API documentation using Lucene.NET with advanced syntax
- **Specialized Queries**: Find code examples, exceptions, and analyze method complexity
- **Cross-References**: Track relationships between types and members
- **Multiple Output Formats**: Table (human), JSON (machine), Markdown (docs)
- **Rich Metadata Extraction**: Code examples, parameter details, exception information
- **Smart Index Management**: Consistent index location with environment variable support
- **Cross-Platform**: Works on Windows, Linux, and macOS via Spectre.IO
- **MCP Ready**: Designed for integration with Model Context Protocol

## Installation

```bash
# Clone the repository
git clone https://github.com/endjin/ApiLens.git
cd ApiLens

# Build the application
dotnet build ./Solutions/ApiLens.Cli/ApiLens.Cli.csproj --configuration Release

# Run the CLI
dotnet run --project ./Solutions/ApiLens.Cli -- --help

# Or build and use the executable directly
./Solutions/ApiLens.Cli/bin/Debug/net9.0/apilens --help
```

## Quick Start

### 🚀 Recommended Workflow

```bash
# 1. Analyze your solution (automatically discovers and indexes all packages)
apilens analyze ./MySolution.sln

# 2. Explore a specific package interactively
apilens explore Newtonsoft.Json

# 3. Query for specific APIs
apilens query JsonSerializer

# 4. Discover type hierarchies
apilens hierarchy JObject --show-members

# 5. Find code examples
apilens examples "async await"
```

### Demo Scripts

Check out the demo scripts in the `Demos/` folder for hands-on examples:

```bash
# Quick start demo
pwsh ./Demos/core/quick-start.ps1

# Project/Solution analysis demo
pwsh ./Demos/core/analyze-demo.ps1

# NuGet package indexing demo  
pwsh ./Demos/nuget/nuget-basic.ps1

# Enhanced drill-down demo
pwsh ./Demos/advanced/enhanced-drilldown-demo.ps1

# Test all demos
cd Demos && pwsh ./test-all.ps1
```

## Index Management

ApiLens uses a smart index location strategy:

1. **Explicit path**: Use `--index /path/to/index` to specify a custom location
2. **Environment variable**: Set `APILENS_INDEX=/path/to/index` for a persistent location
3. **Default**: Uses `~/.apilens/index` in your home directory

```bash
# Use custom index location
apilens index ./docs --index /my/custom/index

# Set environment variable for consistent location
export APILENS_INDEX=/shared/apilens-index
apilens index ./docs  # Uses /shared/apilens-index

# Default location (no configuration needed)
apilens index ./docs  # Uses ~/.apilens/index
```

## Core Commands

### 📦 analyze - Project/Solution Analysis (Recommended Starting Point)

Analyzes .NET projects or solutions to discover and index all their dependencies:

```bash
# Analyze a solution
apilens analyze ./MySolution.sln

# Analyze a project with transitive dependencies
apilens analyze ./MyProject.csproj --include-transitive

# Use project.assets.json for exact versions
apilens analyze ./MyProject.csproj --use-assets

# Clean rebuild of index
apilens analyze ./MySolution.sln --clean

# Get JSON output for automation
apilens analyze ./MyProject.csproj --format json
```

### 🔍 explore - Interactive Package Exploration

Best starting point for understanding a new package:

```bash
# Explore a package structure
apilens explore Newtonsoft.Json

# Show complexity metrics
apilens explore Serilog --show-complexity

# Get JSON output for processing
apilens explore System.Text.Json --format json
```

Shows:
- Package statistics and documentation coverage
- Main namespaces with type counts
- Entry point types (Create, Parse, Load methods)
- Key interfaces
- Most complex types
- Suggested next exploration steps

### 📚 index - Index XML Documentation

Index XML documentation files into a searchable database:

```bash
# Index a directory
apilens index ./docs

# Index with clean rebuild
apilens index ./docs --clean

# Index specific pattern
apilens index ./packages --pattern "**/*.xml"

# Use custom index location
apilens index ./docs --index ./my-index
```

### 🔎 query - Search API Documentation

Powerful search with multiple query types:

```bash
# Search by name (default)
apilens query StringBuilder

# Full-text search in documentation
apilens query "thread safety" --type content

# Search for methods with parameter filtering
apilens query Parse --type method --min-params 1 --max-params 2

# Search by namespace
apilens query "System.Collections.Generic" --type namespace

# Wildcard searches
apilens query "List*"          # Matches List, ListItem, etc.
apilens query "*Exception"     # All exception types

# Boolean searches (operators must be uppercase)
apilens query "async AND await" --type content
apilens query "collection OR list" --type content

# Phrase searches
apilens query "\"extension method\"" --type content
```

### 🏗️ hierarchy - Explore Type Relationships

Discover inheritance chains and interface implementations:

```bash
# Basic hierarchy
apilens hierarchy List

# Show all members
apilens hierarchy Dictionary --show-members

# Include inherited members
apilens hierarchy Exception --show-members --show-inherited

# JSON output for processing
apilens hierarchy IEnumerable --format json
```

### 📝 examples - Find Code Examples

Find and search within code examples:

```bash
# List all methods with examples
apilens examples

# Search for specific patterns
apilens examples "async await"
apilens examples "using statement"
apilens examples "LINQ"

# Get structured data
apilens examples --format json --max 10
```

### ⚠️ exceptions - Find Exception Information

Discover what exceptions methods can throw:

```bash
# Simple exception search
apilens exceptions IOException

# Wildcard patterns
apilens exceptions "*Validation*"
apilens exceptions "Argument*"

# Get detailed information
apilens exceptions ArgumentNullException --details

# JSON output for processing
apilens exceptions "*Exception" --format json --max 20
```

### 📊 complexity - Analyze Method Complexity

Find methods by parameter count and complexity:

```bash
# Find simple methods
apilens complexity --max-params 1

# Find complex signatures
apilens complexity --min-params 5

# Analyze complexity with statistics
apilens complexity --min-complexity 10 --stats

# Find methods in parameter range
apilens complexity --min-params 2 --max-params 4
```

### 👥 members - List Type Members

Show all members of a specific type:

```bash
# List members of a type
apilens members String

# Show with summaries
apilens members List --show-summary

# Deduplicate across versions
apilens members Dictionary --distinct

# JSON output
apilens members IEnumerable --format json
```

### 📋 list-types - Browse Available Types

Browse and list types from packages, namespaces, or assemblies:

```bash
# List types in a package
apilens list-types --package "Newtonsoft.Json"

# Filter by namespace
apilens list-types --namespace "System.Collections.*"

# Combine filters
apilens list-types --package "Microsoft.*" --namespace "*.Logging"

# Include all members, not just types
apilens list-types --package "Serilog" --include-members
```

### 📦 nuget - Index NuGet Cache

Automatically discover and index packages from NuGet cache:

```bash
# List available packages
apilens nuget --list

# Index specific packages
apilens nuget --filter "Microsoft.*"

# Index only latest versions
apilens nuget --latest-only

# Clean rebuild
apilens nuget --clean --filter "System.*"
```

### 📈 stats - Index Statistics

Display index statistics and documentation quality metrics:

```bash
# Basic statistics
apilens stats

# Include documentation metrics
apilens stats --doc-metrics

# JSON output for monitoring
apilens stats --format json
```

## Advanced Features

### Query Filters and Options

Most query commands support advanced filtering:

```bash
# Filter by member type
apilens query "Parse" --member-type Method

# Filter by namespace (wildcards supported)
apilens query "*" --namespace-filter "System.Text.*"

# Filter by assembly
apilens query "*" --assembly-filter "mscorlib"

# Combine multiple filters
apilens query "Create" --member-type Method --namespace-filter "System.*" --min-params 0 --max-params 2

# Sort and limit results
apilens query "Exception" --max 50 --quality-first
```

### Output Formats

All commands support multiple output formats:

```bash
# Table format (default - human readable)
apilens query String

# JSON format (for automation and LLM integration)
apilens query String --format json

# Markdown format (for documentation)
apilens query String --format markdown
```

### Lucene Query Syntax

Full Lucene syntax support for content searches:

```bash
# Wildcards
apilens query "str?ng*" --type content    # ? = single char, * = multiple

# Fuzzy search
apilens query "thred~" --type content     # Finds thread, threads, etc.

# Proximity search
apilens query "\"async method\"~5" --type content  # Words within 5 positions

# Field-specific searches (advanced)
apilens query "summary:thread" --type content
apilens query "remarks:performance" --type content
```

## MCP Integration

ApiLens is designed for Model Context Protocol integration, providing structured JSON output for LLM consumption.

### Example LLM Integration Scenarios

```bash
# Understanding API usage
apilens examples "HttpClient" --format json
apilens query "HttpClient" --type content --format json

# Error handling guidance
apilens exceptions "IOException" --details --format json
apilens examples "try catch" --format json

# API complexity analysis
apilens complexity --max-params 2 --format json    # Simple APIs
apilens complexity --min-params 5 --format json    # Complex APIs

# Package exploration workflow
apilens analyze ./project.csproj --format json
apilens explore "PackageName" --format json
apilens hierarchy "MainType" --show-members --format json
```

## Architecture

The solution is organized under the `Solutions/` directory:

- **ApiLens.Core**: Domain models, parsing, and Lucene.NET integration
- **ApiLens.Cli**: Spectre.Console-based command-line interface
- **Test Projects**: Comprehensive unit tests with TDD approach
  - ApiLens.Core.Tests (510+ tests)
  - ApiLens.Cli.Tests (224+ tests)

### Key Components

1. **XmlDocumentParser**: Parses .NET XML documentation files
2. **LuceneIndexManager**: Manages the Lucene.NET search index with performance optimizations
3. **QueryEngine**: High-level search API with specialized query methods
4. **IndexPathResolver**: Smart index location management
5. **TypeHierarchyResolver**: Discovers type relationships
6. **NuGetCacheScanner**: Discovers and indexes packages from NuGet cache
7. **ProjectAnalysisService**: Analyzes .NET projects and solutions
8. **Specialized Commands**: Examples, Exceptions, Complexity, Explore, etc.

## Development

### Prerequisites

- .NET 9 SDK
- Visual Studio 2022 or VS Code with C# extensions
- PowerShell Core (for running demo scripts)

### Building

```bash
# Build the entire solution
dotnet build ./Solutions/ApiLens.sln

# Build just the CLI application
dotnet build ./Solutions/ApiLens.Cli/ApiLens.Cli.csproj

# Build in Release mode
dotnet build ./Solutions/ApiLens.sln --configuration Release
```

### Running Tests

```bash
# Run all tests
dotnet test ./Solutions/ApiLens.sln

# Run with coverage
dotnet test ./Solutions/ApiLens.sln --collect:"XPlat Code Coverage"

# Run specific test project
dotnet test ./Solutions/ApiLens.Core.Tests/ApiLens.Core.Tests.csproj
```

### Code Style

The project follows strict TDD principles with:
- Test-first development (every feature starts with a failing test)
- Immutable data structures (records and ImmutableArray)
- Functional programming patterns
- Comprehensive XML documentation
- Modern C# 13 features (collection expressions, pattern matching)
- No underscore prefixes for private fields (using `this.` qualification)
- Spectre.IO for all filesystem operations (no System.IO)

## Project Structure

```
ApiLens/
├── Solutions/                    # Main solution files
│   ├── ApiLens.sln              # Solution file
│   ├── ApiLens.Core/            # Core library
│   ├── ApiLens.Core.Tests/      # Core tests (510+ tests)
│   ├── ApiLens.Cli/             # CLI application
│   └── ApiLens.Cli.Tests/       # CLI tests (224+ tests)
├── Demos/                        # Demo scripts
│   ├── core/                    # Basic demos
│   ├── nuget/                   # NuGet demos
│   ├── advanced/                # Advanced demos
│   └── test-all.ps1             # Test runner
├── .devcontainer/               # Dev container configuration
└── README.md                     # This file
```

## Recent Improvements

- **Smart Index Management**: Index location now uses environment variables and home directory defaults
- **Enhanced Package Exploration**: New `explore` command for interactive package discovery
- **Improved Demo Scripts**: All demos updated with consistent path resolution
- **Better Error Messages**: Helpful suggestions when queries return no results
- **Performance Optimizations**: Cached readers, object pooling, parallel processing
- **Cross-Platform Support**: Full Spectre.IO integration for filesystem operations

## License

This project is licensed under the Apache 2.0 License - see the LICENSE file for details.

## Contributing

Contributions are welcome! Please read our contributing guidelines and submit pull requests to our repository.

---

*Built entirely from prompts with Claude Code using Opus 4 & Sonnet 4 models*