# ApiLens

ApiLens is a .NET 9 CLI application that indexes and queries .NET XML API documentation using Lucene.NET. It's designed to make .NET API documentation searchable and accessible, particularly for LLMs through the Model Context Protocol (MCP).

## Features

- **Index XML Documentation**: Parse and index .NET XML documentation files
- **NuGet Package Support**: Automatically discover and index documentation from NuGet cache
- **Version Tracking**: Track and display package versions and target frameworks
- **Full-Text Search**: Query API documentation using Lucene.NET with advanced Lucene syntax
- **Specialized Queries**: Find code examples, exceptions, and analyze method complexity
- **Type Hierarchy**: Discover base types, derived types, and interfaces
- **Cross-References**: Track relationships between types and members
- **Multiple Output Formats**: Table (human), JSON (machine), Markdown (docs)
- **Rich Metadata Extraction**: Code examples, parameter details, exception information
- **Cross-Platform**: Works on Windows, Linux, and macOS
- **MCP Ready**: Designed for integration with Model Context Protocol

## Installation

```bash
# Clone the repository
git clone https://github.com/endjin/ApiLens.git
cd apilens

# Build the application
dotnet build ./Solutions/ApiLens.Cli/ApiLens.Cli.csproj --configuration Release

# Run the CLI
dotnet run --project ./Solutions/ApiLens.Cli -- --help
```

## Quick Start

Check out the demo scripts in the `Demos/` folder for hands-on examples:

```bash
# Quick start demo
pwsh ./Demos/core/quick-start.ps1

# NuGet package indexing demo  
pwsh ./Demos/nuget/nuget-basic.ps1

# Test all demos
cd Demos && pwsh ./test-all.ps1
```

**Note**: All demo scripts create their temporary files under the `/.tmp/` directory:
- Indexes: `/.tmp/indexes/`
- Sample documentation: `/.tmp/docs/`

This keeps demo artifacts separate from your project files and makes cleanup easy.

See [Demos/README.md](Demos/README.md) for a complete list of demonstration scripts.

## Usage

### Indexing Documentation

Index a single XML file:
```bash
apilens index ./MyLibrary.xml
```

Index all XML files in a directory:
```bash
apilens index ./docs
```

Clean and rebuild index:
```bash
apilens index ./docs --clean
```

Use custom index location:
```bash
apilens index ./docs --index ./index/custom-index
```

**Note**: By convention, all indexes are stored under the `./index/` directory. Demo scripts use `/.tmp/indexes/` to keep them separate.

### NuGet Package Indexing

ApiLens can automatically discover and index documentation from your NuGet package cache:

List packages with documentation:
```bash
apilens nuget --list
```

Index specific packages:
```bash
apilens nuget --filter "newtonsoft.*"
```

Index only latest versions:
```bash
apilens nuget --latest-only
```

Index all packages with documentation:
```bash
apilens nuget
```

### Querying the Index

Search by name:
```bash
apilens query String
```

Search in content/documentation:
```bash
apilens query "collection" --type content
```

Search by namespace:
```bash
apilens query "System.Collections" --type namespace
```

Get by exact ID:
```bash
apilens query "T:System.String" --type id
```

Search by assembly:
```bash
apilens query "System.Runtime" --type assembly
```

### Specialized Query Commands

ApiLens provides specialized commands for targeted searches:

#### Code Examples Command
Find methods with code examples or search within example code:

```bash
# List all methods with code examples
apilens examples

# Search for specific patterns in code examples
apilens examples "async" --max 20
apilens examples "parser.ParseFile"
apilens examples "catch" --format json

# Get structured example data for LLM processing
apilens examples --format json --max 5
```

#### Exceptions Command
Find methods that throw specific exceptions:

```bash
# Find methods throwing ArgumentNullException
apilens exceptions "ArgumentNullException"

# Get detailed exception information
apilens exceptions "System.IO.IOException" --details --format markdown

# Search for custom exceptions
apilens exceptions "ValidationException" --max 50
```

#### Complexity Command
Analyze method complexity and parameter counts:

```bash
# Find methods with many parameters
apilens complexity --min-params 5

# Analyze complexity with statistics
apilens complexity --min-complexity 10 --stats

# Find methods within parameter range
apilens complexity --min-params 2 --max-params 4 --sort params

# Get complexity analysis in JSON format
apilens complexity --format json --stats
```

#### Stats Command
Display index statistics and metadata:

```bash
# Show index statistics
apilens stats

# Get statistics in JSON format
apilens stats --format json

# Get statistics in Markdown format
apilens stats --format markdown
```

### Advanced Query Syntax

ApiLens supports full Lucene query syntax for content searches:

```bash
# Wildcard searches
apilens query "string*" --type content    # Matches string, strings, stringify
apilens query "utilit?" --type content    # Matches utility, utilities

# Fuzzy searches
apilens query "tokenze~" --type content   # Finds tokenize, tokenizes, etc.

# Boolean operators (must be uppercase)
apilens query "string AND manipulation" --type content
apilens query "thread OR async" --type content
apilens query "collection NOT list" --type content

# Phrase searches
apilens query "\"extension methods\"" --type content
apilens query "\"strongly typed\"" --type content
```

### Output Formats

All commands support multiple output formats:

Table format (default - human readable):
```bash
apilens query String
apilens examples "async"
apilens exceptions "ArgumentNullException"
```

JSON format (machine processing, LLM integration):
```bash
apilens query String --format json
apilens examples "async" --format json
apilens complexity --format json --stats
```

Markdown format (documentation generation):
```bash
apilens query String --format markdown
apilens exceptions "IOException" --format markdown --details
apilens complexity --format markdown --stats
```

## MCP Integration

ApiLens is designed to be exposed as an MCP tool for LLMs. The JSON output format provides structured data that's easy for LLMs to parse and understand.

### MCP Tool Specification

```json
{
  "name": "apilens",
  "description": "Query .NET API documentation with specialized search capabilities",
  "version": "1.0.0",
  "commands": {
    "search": {
      "description": "Search for .NET types, methods, and documentation",
      "parameters": {
        "query": {
          "type": "string",
          "description": "Search query (supports Lucene syntax for content searches)",
          "required": true
        },
        "type": {
          "type": "string",
          "enum": ["name", "content", "namespace", "id", "assembly"],
          "description": "Type of search to perform",
          "default": "name"
        },
        "max": {
          "type": "integer",
          "description": "Maximum results to return",
          "default": 10
        },
        "format": {
          "type": "string",
          "enum": ["table", "json", "markdown"],
          "description": "Output format",
          "default": "json"
        }
      }
    },
    "examples": {
      "description": "Find code examples or search within example code",
      "parameters": {
        "pattern": {
          "type": "string",
          "description": "Pattern to search for in code examples (optional)",
          "required": false
        },
        "max": {
          "type": "integer",
          "description": "Maximum results to return",
          "default": 10
        },
        "format": {
          "type": "string",
          "enum": ["table", "json", "markdown"],
          "description": "Output format",
          "default": "json"
        }
      }
    },
    "exceptions": {
      "description": "Find methods that throw specific exceptions",
      "parameters": {
        "exception_type": {
          "type": "string",
          "description": "Exception type to search for (e.g., ArgumentNullException)",
          "required": true
        },
        "details": {
          "type": "boolean",
          "description": "Show detailed exception information",
          "default": false
        },
        "max": {
          "type": "integer",
          "description": "Maximum results to return",
          "default": 10
        },
        "format": {
          "type": "string",
          "enum": ["table", "json", "markdown"],
          "description": "Output format",
          "default": "json"
        }
      }
    },
    "complexity": {
      "description": "Analyze method complexity and parameter counts",
      "parameters": {
        "min_complexity": {
          "type": "integer",
          "description": "Minimum complexity threshold"
        },
        "min_params": {
          "type": "integer",
          "description": "Minimum parameter count"
        },
        "max_params": {
          "type": "integer",
          "description": "Maximum parameter count"
        },
        "stats": {
          "type": "boolean",
          "description": "Include statistical analysis",
          "default": false
        },
        "sort": {
          "type": "string",
          "enum": ["complexity", "params"],
          "description": "Sort results by complexity or parameter count",
          "default": "complexity"
        },
        "max": {
          "type": "integer",
          "description": "Maximum results to return",
          "default": 20
        },
        "format": {
          "type": "string",
          "enum": ["table", "json", "markdown"],
          "description": "Output format",
          "default": "json"
        }
      }
    },
    "stats": {
      "description": "Display index statistics and metadata",
      "parameters": {
        "format": {
          "type": "string",
          "enum": ["table", "json", "markdown"],
          "description": "Output format",
          "default": "table"
        }
      }
    }
  }
}
```

### Example MCP Usage

```bash
# Basic API search
apilens query "string" --format json --max 20
apilens query "T:System.String" --type id --format json
apilens query "System.Collections.Generic" --type namespace --format json

# Specialized searches for LLM integration
apilens examples "async" --format json --max 10
apilens exceptions "ArgumentNullException" --format json
apilens complexity --min-params 3 --format json --stats

# Advanced content searches with Lucene syntax
apilens query "thread* AND safe" --type content --format json
apilens query "collection OR list" --type content --format json --max 15

# Index statistics and metadata
apilens stats --format json
```

### Real-World LLM Integration Scenarios

1. **Understanding API Usage Patterns**:
   ```bash
   # LLM discovers how to use a specific API
   apilens examples "HttpClient" --format json
   apilens query "HttpClient" --type content --format json
   ```

2. **Error Handling Guidance**:
   ```bash
   # LLM finds what exceptions to handle
   apilens exceptions "IOException" --details --format json
   apilens examples "try catch" --format json
   ```

3. **API Complexity Analysis**:
   ```bash
   # LLM identifies simple vs complex APIs
   apilens complexity --max-params 2 --format json    # Simple APIs
   apilens complexity --min-params 5 --format json    # Complex APIs
   ```

## Architecture

The solution is organized under the `Solutions/` directory:

- **ApiLens.Core**: Domain models, parsing, and Lucene.NET integration
- **ApiLens.Cli**: Spectre.Console-based command-line interface
- **Test Projects**: Comprehensive unit tests with TDD approach
  - ApiLens.Core.Tests
  - ApiLens.Cli.Tests
  - ApiLens.Benchmarks

### Key Components

1. **XmlDocumentParser**: Parses .NET XML documentation files
2. **LuceneIndexManager**: Manages the Lucene.NET search index
3. **QueryEngine**: High-level search API
4. **TypeHierarchyResolver**: Discovers type relationships
5. **RelatedTypeResolver**: Finds all types related to a member
6. **NuGetCacheScanner**: Discovers and indexes packages from NuGet cache
7. **Specialized Commands**: Examples, Exceptions, and Complexity analyzers

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
```

### Running Tests

```bash
# Run all tests
dotnet test ./Solutions/ApiLens.sln

# Run tests excluding integration tests
dotnet test ./Solutions/ApiLens.sln --filter "TestCategory!=Integration"

# Run specific test project
dotnet test ./Solutions/ApiLens.Core.Tests/ApiLens.Core.Tests.csproj
```

### Code Coverage

The project uses dotnet-coverage for code coverage measurement. Current coverage thresholds:
- Line coverage: 75%
- Branch coverage: 60%

The project maintains comprehensive test coverage with over 490 tests across all projects.

#### Running Coverage Locally

```bash
# Windows PowerShell
./run-coverage.ps1

# Linux/macOS
./run-coverage.sh
```

This will:
1. Run all unit tests with coverage collection
2. Generate an HTML coverage report in `coverage-report/`
3. Display coverage summary in the console
4. Check if coverage meets the required thresholds

#### Coverage Tools

The project includes the following coverage tools:
- **dotnet-coverage**: Collects code coverage data
- **ReportGenerator**: Generates coverage reports in various formats
- **Coverlet**: Cross-platform coverage collection

#### Viewing Coverage Reports

After running coverage, open `coverage-report/index.html` in your browser to see detailed coverage information including:
- Line-by-line coverage highlighting
- Branch coverage details
- Per-assembly and per-class metrics
- Historical coverage trends (when using CI)

### Code Style

The project follows strict TDD principles with:
- Test-first development (every feature starts with a failing test)
- Immutable data structures (using records and ImmutableArray)
- Functional programming patterns
- Comprehensive XML documentation
- Modern C# features (collection expressions, pattern matching)
- No underscore prefixes for private fields (using `this.` qualification)

See `.editorconfig` for detailed code style settings.

## Project Structure

```
apilens/
├── Solutions/                    # Main solution files
│   ├── ApiLens.sln              # Solution file
│   ├── ApiLens.Core/            # Core library
│   ├── ApiLens.Core.Tests/      # Core tests
│   ├── ApiLens.Cli/             # CLI application
│   ├── ApiLens.Cli.Tests/       # CLI tests
│   └── ApiLens.Benchmarks/      # Performance benchmarks
├── Demos/                        # Demo scripts
│   ├── core/                    # Basic demos
│   ├── nuget/                   # NuGet demos
│   ├── advanced/                # Advanced demos
│   └── test-all.ps1             # Test runner
├── index/                        # Default index location (gitignored)
├── .tmp/                         # Temporary files and demo data (gitignored)
│   ├── indexes/                  # Demo script indexes
│   └── docs/                     # Demo sample documentation
├── coverage-report/              # Coverage reports (gitignored)
└── README.md                     # This file
```

## License

This project is licensed under the Apache 2.0 License - see the LICENSE file for details.

## Contributing

Contributions are welcome! Please read our contributing guidelines and submit pull requests to our repository.

---

*Built entirely from prompts with Claude Code using Opus 4 & Sonnet 4 models*