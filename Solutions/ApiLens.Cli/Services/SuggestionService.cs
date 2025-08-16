using ApiLens.Core.Models;
using ApiLens.Core.Querying;

namespace ApiLens.Cli.Services;

/// <summary>
/// Provides intelligent suggestions when queries return no results.
/// </summary>
public class SuggestionService
{
    private readonly IQueryEngine queryEngine;

    public SuggestionService(IQueryEngine queryEngine)
    {
        ArgumentNullException.ThrowIfNull(queryEngine);
        this.queryEngine = queryEngine;
    }

    /// <summary>
    /// Gets similar names based on fuzzy matching of the query.
    /// </summary>
    /// <param name="query">The original query that returned no results</param>
    /// <param name="queryType">The type of query that was performed</param>
    /// <param name="maxSuggestions">Maximum number of suggestions to return</param>
    /// <returns>List of similar names that might be what the user was looking for</returns>
    public List<string> GetSimilarNames(string query, QueryType queryType, int maxSuggestions = 3)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        try
        {
            // Use fuzzy search by appending ~ to the query
            string fuzzyQuery = $"{query}~";
            List<MemberInfo> fuzzyResults = queryType switch
            {
                QueryType.Name => queryEngine.SearchByName(fuzzyQuery, maxSuggestions * 3),
                QueryType.Namespace => queryEngine.SearchByNamespace(fuzzyQuery, maxSuggestions * 3),
                QueryType.Assembly => queryEngine.SearchByAssembly(fuzzyQuery, maxSuggestions * 3),
                _ => queryEngine.SearchByContent(fuzzyQuery, maxSuggestions * 3)
            };

            // Extract unique names based on query type
            return fuzzyResults
                .Select(r => queryType switch
                {
                    QueryType.Name => r.Name,
                    QueryType.Namespace => r.Namespace,
                    QueryType.Assembly => r.Assembly,
                    _ => r.Name
                })
                .Distinct()
                .Take(maxSuggestions)
                .ToList();
        }
        catch
        {
            // If fuzzy search fails, return empty list
            return [];
        }
    }

    /// <summary>
    /// Gets a helpful hint based on the query type.
    /// </summary>
    /// <param name="queryType">The type of query that was performed</param>
    /// <returns>A helpful hint message for the user</returns>
    public static string GetSearchHint(QueryType queryType)
    {
        return queryType switch
        {
            QueryType.Name => "Try using wildcards (*) for partial matches, or search by content instead.",
            QueryType.Content => "Try broadening your search terms or using wildcards (*,?).",
            QueryType.Namespace => "Ensure the namespace exists. Try partial matching with wildcards (*).",
            QueryType.Id => "Member IDs must be exact. Use the query command to find the correct ID.",
            QueryType.Assembly => "Check the assembly name. Use wildcards (*) for partial matches.",
            _ => "Try broadening your search or check the index status with 'apilens stats'."
        };
    }

    /// <summary>
    /// Gets example queries that might help the user.
    /// </summary>
    /// <param name="queryType">The type of query that was performed</param>
    /// <param name="originalQuery">The original query that failed</param>
    /// <returns>List of example queries</returns>
    public static List<string> GetExampleQueries(QueryType queryType, string originalQuery)
    {
        List<string> examples = [];

        if (string.IsNullOrWhiteSpace(originalQuery))
        {
            return examples;
        }

        switch (queryType)
        {
            case QueryType.Name:
                examples.Add($"apilens query \"{originalQuery}*\" --type name");
                examples.Add($"apilens query \"{originalQuery}\" --type content");
                break;

            case QueryType.Namespace:
                string namespacePart = originalQuery.Contains('.')
                    ? originalQuery[..originalQuery.LastIndexOf('.')]
                    : originalQuery;
                examples.Add($"apilens query \"{namespacePart}*\" --type namespace");
                examples.Add($"apilens list-types --namespace \"{originalQuery}*\"");
                break;

            case QueryType.Assembly:
                examples.Add($"apilens query \"*{originalQuery}*\" --type assembly");
                examples.Add($"apilens list-types --assembly \"{originalQuery}*\"");
                break;

            case QueryType.Content:
                examples.Add($"apilens query \"{originalQuery}~\" --type content");
                examples.Add($"apilens query \"{originalQuery} OR {originalQuery}s\" --type content");
                break;
        }

        return examples;
    }

    /// <summary>
    /// Formats a complete suggestion message with similar names and hints.
    /// </summary>
    public string FormatSuggestionMessage(string query, QueryType queryType, List<string>? similarNames = null)
    {
        List<string> messageParts = [];

        // Add the main hint
        messageParts.Add(GetSearchHint(queryType));

        // Add similar names if found
        if (similarNames?.Any() == true)
        {
            messageParts.Add($"\nDid you mean: {string.Join(", ", similarNames.Select(n => $"'{n}'"))}?");
        }

        // Add example queries
        List<string> examples = GetExampleQueries(queryType, query);
        if (examples.Any())
        {
            messageParts.Add("\nTry these queries:");
            foreach (string example in examples)
            {
                messageParts.Add($"  {example}");
            }
        }

        return string.Join("\n", messageParts);
    }
}

/// <summary>
/// Query type enumeration for suggestion service.
/// </summary>
public enum QueryType
{
    Name,
    Content,
    Namespace,
    Id,
    Assembly,
    Method,
    Package,
    ListTypes
}