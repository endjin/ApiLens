# ApiLens CLI

A .NET global tool for indexing and querying .NET XML API documentation using Lucene.NET.

## Installation

```bash
dotnet tool install --global apilens
```

## Quick Start

```bash
# Index documentation
apilens index ./docs

# Search for a type
apilens query String

# Find methods throwing exceptions
apilens exceptions ArgumentNullException

# Find code examples
apilens examples async
```

## Features

- **Index XML Documentation**: Parse and index .NET XML documentation files
- **NuGet Package Support**: Automatically discover and index documentation from NuGet cache
- **Full-Text Search**: Query API documentation using Lucene.NET
- **Specialized Queries**: Find code examples, exceptions, and analyze method complexity
- **Multiple Output Formats**: Table (human), JSON (machine), Markdown (docs)
- **MCP Ready**: Designed for integration with Model Context Protocol

## Commands

### Index Command
Index XML documentation files or directories:
```bash
apilens index <path> [options]
```

### NuGet Command
Index documentation from NuGet packages:
```bash
apilens nuget [options]
  --list              List packages with documentation
  --filter <pattern>  Filter packages by pattern
  --latest-only       Index only latest versions
```

### Query Command
Search the indexed documentation:
```bash
apilens query <search-term> [options]
  --type <type>       Search type: name, content, namespace, id, assembly
  --max <count>       Maximum results (default: 10)
  --format <format>   Output format: table, json, markdown
```

### Exceptions Command
Find methods that throw specific exceptions:
```bash
apilens exceptions <exception-type> [options]
  --details           Show detailed information
  --max <count>       Maximum results (default: 10)
  --format <format>   Output format: table, json, markdown
```

### Examples Command
Find methods with code examples:
```bash
apilens examples [pattern] [options]
  --max <count>       Maximum results (default: 10)
  --format <format>   Output format: table, json, markdown
```

### Complexity Command
Analyze method complexity:
```bash
apilens complexity [options]
  --min-params <n>    Minimum parameter count
  --max-params <n>    Maximum parameter count
  --min-complexity <n> Minimum complexity
  --stats             Show statistics
  --sort <by>         Sort by: complexity, params
  --format <format>   Output format: table, json, markdown
```

### Stats Command
Display index statistics:
```bash
apilens stats [options]
  --format <format>   Output format: table, json, markdown
```

## Advanced Search

ApiLens supports Lucene query syntax for content searches:

- **Wildcards**: `string*`, `utilit?`
- **Fuzzy**: `tokenze~`
- **Boolean**: `string AND manipulation`, `thread OR async`
- **Phrases**: `"extension methods"`

## MCP Integration

ApiLens is designed for Model Context Protocol integration. Use JSON output format for structured data:

```bash
apilens query String --format json
apilens examples async --format json --max 10
apilens exceptions IOException --format json
```

## Documentation

For more information, visit: https://github.com/endjin/ApiLens

## License

Licensed under the Apache License, Version 2.0