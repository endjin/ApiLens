namespace ApiLens.Cli;

/// <summary>
/// Centralized help text and guidance for CLI commands.
/// </summary>
internal static class HelpText
{
    public const string ApplicationDescription = """
        .NET API Documentation Explorer and Search Tool
        
        A powerful command-line tool for indexing and searching .NET XML documentation.
        Perfect for exploring APIs, understanding codebases, and discovering implementations.
        
        QUICK START:
          1. Index a solution: apilens analyze ./MySolution.sln
          2. Search for types: apilens query "ClassName"
          3. Explore methods: apilens query "MethodName" --type method
          4. View hierarchy: apilens hierarchy "TypeName"
        
        LLM USAGE GUIDE:
          1. Start with 'analyze' to index a solution/project automatically
          2. Use 'stats --doc-metrics' to assess documentation quality
          3. Search with 'query' using the appropriate --type:
             - name: Find exact type/method names (default)
             - content: Full-text search in documentation
             - method: Find methods with parameter filtering
          4. Explore type relationships with 'hierarchy'
          5. Browse package contents with 'list-types'
          6. Always use --format json for structured data parsing
          
        COMMON WORKFLOWS:
          Explore a Package: analyze → list-types → query → hierarchy
          Find Implementations: query (interface) → hierarchy → list-types
          Understand Methods: query --type method → examples → exceptions
        """;

    public const string IndexCommandDescription = """
        Index XML documentation files into a searchable database.
        
        This is the foundation command - use it to build your searchable API index.
        Supports incremental updates or clean rebuilds with --clean flag.
        
        WHEN TO USE:
          - First time setup: Index your XML documentation files
          - After updates: Re-run to add new documentation
          - Custom sources: Index specific XML files or directories
        
        TIP: For projects/solutions, use 'analyze' command instead for automatic package discovery.
        """;

    public const string QueryCommandDescription = """
        Query the API documentation index with powerful search capabilities.
        
        QUERY TYPES EXPLAINED:
          name (default) - Exact match for type/method names
            Use when: You know the exact name
            Example: apilens query "StringBuilder"
          
          content - Full-text search in documentation
            Use when: Searching for concepts or keywords
            Example: apilens query "thread safety" --type content
          
          method - Search for methods with parameter filtering
            Use when: Finding methods by signature
            Example: apilens query "Parse" --type method --min-params 1
          
          namespace - Find all types in a namespace
            Use when: Exploring a specific namespace
            Example: apilens query "System.Collections.Generic" --type namespace
          
          id - Direct lookup by member ID
            Use when: You have an exact reference
            Example: apilens query "T:System.String" --type id
        
        SEARCH SYNTAX:
          Wildcards: * (multiple), ? (single) - Example: List*
          Fuzzy: ~ for similar terms - Example: roam~
          Boolean: AND, OR, NOT - Example: async AND task
          Phrases: "exact phrase" - Example: "extension method"
        
        FILTERING OPTIONS:
          --member-type: Filter by Type, Method, Property, Field, Event
          --namespace: Filter by namespace pattern (wildcards supported)
          --assembly: Filter by assembly pattern (wildcards supported)
          --min-params/--max-params: Filter methods by parameter count
        
        PRO TIP: Combine filters for precise results!
        """;

    public const string AnalyzeCommandDescription = """
        RECOMMENDED STARTING POINT - Analyze and index all packages from a project or solution.
        
        This command combines package discovery with automatic indexing - the easiest way to start!
        It parses your project/solution, finds all NuGet packages, and indexes their documentation.
        
        WORKFLOW:
          1. Run: apilens analyze ./YourSolution.sln
          2. Query: apilens query "TypeYouNeed"
          3. Explore: apilens hierarchy "TypeYouFound"
        
        OPTIONS EXPLAINED:
          --include-transitive: Also index indirect dependencies
          --use-assets: Use project.assets.json for exact versions
          --clean: Start fresh (removes existing index)
        
        SUPPORTED FILES:
          - Solution files: .sln
          - C# projects: .csproj
          - F# projects: .fsproj
          - VB projects: .vbproj
        """;

    public const string HierarchyCommandDescription = """
        Explore type hierarchies including inheritance and interface relationships.
        
        Discover type relationships:
          - Base classes (inheritance chain)
          - Implemented interfaces
          - Derived types (who inherits from this)
          - Type members (with --show-members)
        
        USE CASES:
          - Understanding inheritance: See the complete type hierarchy
          - Finding implementations: Discover all types implementing an interface
          - API exploration: View all members of a type
        
        EXAMPLES FOR LLMS:
          Basic: apilens hierarchy "List"
          With members: apilens hierarchy "Dictionary" --show-members
          JSON output: apilens hierarchy "IEnumerable" --format json
        
        NOTE: Best results with exact type names. Use 'query' first to find the correct name.
        """;

    public const string ListTypesCommandDescription = """
        Browse and list types from assemblies, packages, or namespaces.
        
        This is your browsing tool - use it to explore what's available in packages.
        
        FILTERING STRATEGY:
          Start broad, then narrow:
          1. List all types in a package: --package "PackageName"
          2. Filter by namespace: --namespace "Specific.Namespace"
          3. Add assembly filter if needed: --assembly "AssemblyName"
        
        WILDCARD SUPPORT:
          All filters support wildcards (* and ?):
          --package "Microsoft.*" - All Microsoft packages
          --namespace "System.Collections.*" - All collections namespaces
          --assembly "*.Core" - All Core assemblies
        
        DRILL-DOWN PATTERN:
          Package → Namespace → Type → Members
          1. apilens list-types --package "Newtonsoft.Json"
          2. apilens list-types --package "Newtonsoft.Json" --namespace "Newtonsoft.Json.Linq"
          3. apilens query "JObject" (to get specific type)
          4. apilens hierarchy "JObject" --show-members
        
        TIP: Use --include-members to see all members, not just types.
        """;

    public const string ExamplesCommandDescription = """
        Find and display code examples from XML documentation.
        
        Code examples are gold! This command helps you find actual usage patterns.
        
        SEARCH STRATEGIES:
          - No arguments: List all methods with examples
          - With pattern: Find examples containing specific code
          
        USE CASES:
          - Learning APIs: See how to use unfamiliar methods
          - Best practices: Find recommended usage patterns
          - Testing: Discover example test cases
        
        OUTPUT:
          Shows the full code example with syntax highlighting.
          Use --format json to extract examples programmatically.
        """;

    public const string ExceptionsCommandDescription = """
        Find methods that throw specific exceptions.
        
        Understand error handling by discovering what exceptions methods can throw.
        
        SEARCH PATTERNS:
          - Full name: "System.ArgumentNullException"
          - Partial name: "IOException"
          - Wildcards: "*Exception" (all exceptions)
          - Leading wildcards: "*Validation*" (anything with Validation)
        
        USE CASES:
          - Error handling: Know what exceptions to catch
          - API safety: Understand failure modes
          - Documentation: Generate exception documentation
        
        OPTIONS:
          --details: Show full exception documentation
          --format json: Get structured exception data
        """;

    public const string ComplexityCommandDescription = """
        Analyze method complexity and find methods by parameter count.
        
        Understand API complexity and find methods matching specific signatures.
        
        FILTER OPTIONS:
          --min-params N: Methods with at least N parameters
          --max-params N: Methods with at most N parameters
          --min-complexity N: Methods with cyclomatic complexity ≥ N
        
        USE CASES:
          - Find simple methods: --max-params 1
          - Find complex signatures: --min-params 5
          - Identify refactoring targets: --min-complexity 10
        
        STATISTICS:
          Use --stats to get aggregate metrics about complexity.
          Great for assessing overall API complexity!
        """;

    public const string StatsCommandDescription = """
        Display comprehensive index statistics and documentation quality metrics.
        
        Get insights into your indexed API documentation.
        
        METRICS PROVIDED:
          - Index size and document count
          - Documentation coverage percentage
          - Average documentation quality score
          - Types needing documentation improvement
        
        DOCUMENTATION QUALITY:
          Use --doc-metrics for detailed quality analysis:
          - Coverage: What percentage has documentation
          - Quality: How complete is the documentation
          - Examples: How many methods have code examples
        
        USE IN WORKFLOWS:
          Run after indexing to verify success and assess quality.
          Use JSON output for automated quality gates.
        """;

    public const string NuGetCommandDescription = """
        Scan and index your NuGet package cache.
        
        Automatically discovers and indexes all packages in your NuGet cache.
        Perfect for exploring all available packages on your system.
        
        FILTERING:
          --filter "pattern": Only index matching packages
          --latest-only: Skip older versions
        
        DISCOVERY:
          --list: Show packages without indexing
          --filter with --list: Preview what would be indexed
        
        COMMON PATTERNS:
          Index everything: apilens nuget
          Microsoft packages: apilens nuget --filter "Microsoft.*" --latest-only
          List available: apilens nuget --list --filter "System.*"
        """;

    // Workflow examples for LLMs
    public const string WorkflowExamples = """
        COMPLETE WORKFLOW EXAMPLES:
        
        1. EXPLORE A NUGET PACKAGE:
           apilens analyze ./MySolution.sln --index ./my-index
           apilens list-types --package "PackageName*" --index ./my-index
           apilens query "InterestingType" --index ./my-index
           apilens hierarchy "InterestingType" --show-members --index ./my-index
        
        2. FIND METHOD IMPLEMENTATIONS:
           apilens query "MethodName" --type method --index ./my-index
           apilens query "MethodName" --type method --min-params 2 --index ./my-index
           apilens examples "MethodName" --index ./my-index
           apilens exceptions "*Exception" --index ./my-index
        
        3. UNDERSTAND AN INTERFACE:
           apilens query "IMyInterface" --index ./my-index
           apilens hierarchy "IMyInterface" --index ./my-index
           apilens query "IMyInterface" --type content --index ./my-index
        
        4. EXPLORE A NAMESPACE:
           apilens query "System.Collections" --type namespace --index ./my-index
           apilens list-types --namespace "System.Collections.*" --index ./my-index
           apilens complexity --min-params 2 --index ./my-index
        
        5. ASSESS API QUALITY:
           apilens analyze ./Project.csproj --index ./my-index
           apilens stats --doc-metrics --index ./my-index
           apilens examples --index ./my-index
           apilens list-types --package "*" --format json --index ./my-index
        """;

    // Error recovery guidance
    public const string TroubleshootingGuide = """
        TROUBLESHOOTING COMMON ISSUES:
        
        "No results found":
          - Try broader search terms or wildcards (*)
          - Check the query type (--type content for text search)
          - Verify the index exists (apilens stats)
          - Use fuzzy search (~) for typos
        
        "Index not found":
          - Run 'apilens analyze' or 'apilens index' first
          - Check the index path with --index parameter
          - Default index location is ./index
        
        "Duplicate entries":
          - This is normal for multi-targeted packages
          - Results show different framework versions
          - Focus on the framework version you need
        
        "Method search not working":
          - Use --type method explicitly
          - Check parameter filters (--min-params, --max-params)
          - Try searching by method name only first
        
        "No documentation found":
          - Some packages don't include XML documentation
          - Check package version - try newer versions
          - Use --doc-metrics to assess documentation quality
        """;
}