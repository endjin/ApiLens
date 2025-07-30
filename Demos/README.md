# ApiLens Demo Scripts

This directory contains demonstration scripts showcasing various features and capabilities of ApiLens, organized by category.

## 📁 Directory Structure

```
Demos/
├── core/          # Core ApiLens functionality demos
├── nuget/         # NuGet package indexing and analysis demos
├── advanced/      # Advanced features and integrations
└── test-all.ps1   # Test runner for all demos
```

## 🚀 Quick Start

To run any demo, navigate to the repository root and execute:

```powershell
# Run a specific demo
./Demos/core/quick-start.ps1

# Test all demos
./Demos/test-all.ps1

# Test Demos in a specific category
./Demos/test-all.ps1 -Category nuget
```

## 📚 Demo Categories

### Core Demos (`core/`)

Essential ApiLens functionality demonstrations.

| Script             | Description                                                   |
|--------------------|---------------------------------------------------------------|
| `basic-usage.ps1`  | Comprehensive introduction to ApiLens commands and features   |
| `quick-start.ps1`  | Quick 5-minute demo of core functionality                     |
| `version-info.ps1` | Demonstrates version information tracking from NuGet packages |

### NuGet Demos (`nuget/`)

NuGet package cache scanning and analysis features.

| Script                     | Description                                     |
|----------------------------|-------------------------------------------------|
| `nuget-basic.ps1`          | Simple NuGet command usage examples             |
| `nuget-command.ps1`        | Complete NuGet command feature showcase         |
| `nuget-cache-indexing.ps1` | Comprehensive NuGet cache indexing and querying |
| `nuget-scanner.ps1`        | Low-level NuGetCacheScanner functionality       |
| `version-comparison.ps1`   | Compare API versions across different packages  |

### Advanced Demos (`advanced/`)

Advanced features, integrations, and specialized use cases.

| Script                    | Description                                               |
|---------------------------|-----------------------------------------------------------|
| `rich-metadata.ps1`       | Rich metadata extraction for code examples and complexity |
| `specialized-queries.ps1` | Specialized commands (examples, exceptions, complexity)   |
| `mcp-integration.ps1`     | Model Context Protocol (MCP) server integration           |
| `benchmark.ps1`           | Performance benchmarking and optimization analysis        |

## 🛠️ Prerequisites

1. **Build ApiLens**: Ensure ApiLens is built before running demos:
   ```powershell
   dotnet build -c Release
   ```

2. **PowerShell**: All demos require PowerShell Core (pwsh) or Windows PowerShell

3. **NuGet Packages**: Some demos work best with NuGet packages in your cache. The demos will automatically restore common packages like `Newtonsoft.Json` if needed.

## 🧪 Testing Demos

The `test-all.ps1` script validates all demos work correctly:

```powershell
# Test all demos
./Demos/test-all.ps1

# Test specific category
./Demos/test-all.ps1 -Category core
./Demos/test-all.ps1 -Category nuget
./Demos/test-all.ps1 -Category advanced

# Verbose output for debugging
./Demos/test-all.ps1 -Verbose
```

The test script will:
- ✅ Verify each demo runs without errors
- ⏱️ Report execution time
- 🔍 Check for expected features (version info, NuGet commands)
- 📊 Provide a summary of results

## 💡 Demo Highlights

### Version Information Tracking
Many demos showcase ApiLens's ability to track and display version information from NuGet packages:
- Package ID and version
- Target framework
- Source file paths
- Cross-version API comparison

### Cross-Platform Support
All demos work on:
- Windows (PowerShell & PowerShell Core)
- Linux (PowerShell Core)
- macOS (PowerShell Core)

### Real-World Use Cases
- **API Discovery**: Find types, methods, and namespaces across packages
- **Version Analysis**: Compare APIs across different versions
- **Documentation Search**: Full-text search through XML documentation
- **Integration**: Export data for use with LLMs via MCP

## 🤝 Contributing

When adding new demos:
1. Use lowercase names with hyphens (e.g., `my-new-demo.ps1`)
2. Add appropriate category placement
3. Include synopsis and description in script header
4. Update this README with the new demo
5. Ensure the demo passes `test-all.ps1`

## 📝 Notes

- Demos create temporary indexes that are cleaned up automatically
- Some demos require internet access to restore NuGet packages
- The benchmark demo is skipped in regular testing due to long runtime
- All paths in demos are relative to the repository root for portability