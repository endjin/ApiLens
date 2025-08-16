# ApiLens

A .NET global tool for indexing and querying .NET XML API documentation using Lucene.NET.

## Installation

```bash
dotnet tool install --global ApiLens
```

## Quick Start

```bash
# Analyze your solution (indexes all dependencies)
apilens analyze ./MySolution.sln

# Explore a package interactively
apilens explore Newtonsoft.Json

# Search for APIs
apilens query JsonSerializer

# Find code examples
apilens examples "async await"

# Discover type hierarchies
apilens hierarchy Exception --show-members
```

## Key Features

- **Solution Analysis**: Analyzes .NET projects/solutions and indexes all dependencies
- **NuGet Support**: Auto-discovers and indexes packages from NuGet cache
- **Smart Indexing**: Consistent index location with environment variable support
- **Rich Queries**: Full-text search, wildcards, type hierarchies, complexity analysis
- **Multiple Formats**: Table (human), JSON (machine), Markdown (docs)
- **MCP Ready**: Designed for LLM integration via Model Context Protocol

## Core Commands

- `analyze` - Analyze and index project/solution dependencies
- `explore` - Interactive package exploration
- `index` - Index XML documentation files
- `query` - Search API documentation
- `hierarchy` - Explore type relationships
- `examples` - Find code examples
- `exceptions` - Find exception information
- `complexity` - Analyze method complexity
- `members` - List type members
- `list-types` - Browse available types
- `nuget` - Index NuGet cache
- `stats` - Display index statistics

## Index Management

ApiLens stores its index in a consistent location:
- Custom: `--index /path/to/index`
- Environment: `APILENS_INDEX=/path/to/index`
- Default: `~/.apilens/index`

## Documentation

Full documentation and source code: https://github.com/endjin/ApiLens

## License

Apache 2.0