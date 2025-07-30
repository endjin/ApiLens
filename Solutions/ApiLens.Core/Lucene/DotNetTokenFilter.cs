using Lucene.Net.Analysis;
using Lucene.Net.Analysis.TokenAttributes;

namespace ApiLens.Core.Lucene;

internal sealed class DotNetTokenFilter : TokenFilter
{
    private readonly ICharTermAttribute termAttr;
    private readonly IPositionIncrementAttribute posIncrAttr;
    private readonly Queue<string> pendingTokens = new();

    public DotNetTokenFilter(TokenStream input) : base(input)
    {
        termAttr = AddAttribute<ICharTermAttribute>();
        posIncrAttr = AddAttribute<IPositionIncrementAttribute>();
    }

    public override bool IncrementToken()
    {
        // If we have pending tokens, emit them
        if (pendingTokens.Count > 0)
        {
            ClearAttributes();
            termAttr.SetEmpty().Append(pendingTokens.Dequeue());
            posIncrAttr.PositionIncrement = 0; // Same position for all tokens
            return true;
        }

        // Get next token from input
        if (!m_input.IncrementToken())
        {
            return false;
        }

        string fullName = termAttr.ToString();

        // Generate tokens for the full name and its parts
        GenerateTokens(fullName);

        // Emit the first token
        if (pendingTokens.Count > 0)
        {
            ClearAttributes();
            termAttr.SetEmpty().Append(pendingTokens.Dequeue());
            posIncrAttr.PositionIncrement = 1; // First token has position increment 1
            return true;
        }

        return false;
    }

    private void GenerateTokens(string fullName)
    {
        // First, handle generic types with angle brackets
        if (fullName.Contains('<'))
        {
            GenerateGenericTokens(fullName);
            return;
        }

        // Handle generic types with backtick notation
        if (fullName.Contains('`'))
        {
            GenerateBacktickGenericTokens(fullName);
            return;
        }

        // Split on dots
        string[] parts = fullName.Split('.');

        // If only one part (no dots), just emit that part
        if (parts.Length == 1)
        {
            pendingTokens.Enqueue(fullName);
            return;
        }

        // Add the full name first
        pendingTokens.Enqueue(fullName);

        // Add individual parts
        foreach (string part in parts)
        {
            if (!string.IsNullOrEmpty(part))
            {
                pendingTokens.Enqueue(part);
            }
        }

        // Add hierarchical combinations (e.g., "System.Collections", "Collections.Generic")
        for (int i = 0; i < parts.Length - 1; i++)
        {
            for (int j = i + 1; j <= parts.Length; j++)
            {
                string combination = string.Join(".", parts, i, j - i);
                if (combination != fullName && j - i > 1) // Don't duplicate full name or single parts
                {
                    pendingTokens.Enqueue(combination);
                }
            }
        }
    }

    private void GenerateGenericTokens(string fullName)
    {
        // Find the start of generic parameters
        int genericStart = fullName.IndexOf('<');
        string baseType = fullName[..genericStart];

        // Add the full generic type
        pendingTokens.Enqueue(fullName);

        // Now process the base type (which might have dots)
        string[] parts = baseType.Split('.');

        // If the base type has dots (namespace), process it
        if (parts.Length > 1)
        {
            // Add individual parts
            foreach (string part in parts)
            {
                if (!string.IsNullOrEmpty(part))
                {
                    pendingTokens.Enqueue(part);
                }
            }

            // Add the last part with generic parameters
            string lastPart = parts[^1];
            string genericSuffix = fullName[genericStart..];
            pendingTokens.Enqueue(lastPart + genericSuffix);

            // Add hierarchical combinations
            for (int i = 0; i < parts.Length - 1; i++)
            {
                for (int j = i + 1; j <= parts.Length; j++)
                {
                    string combination = string.Join(".", parts, i, j - i);
                    if (j == parts.Length)
                    {
                        // If it's the full namespace, add with generic parameters
                        pendingTokens.Enqueue(combination + genericSuffix);
                    }
                    else if (j - i > 1)
                    {
                        // Otherwise just add the namespace combination
                        pendingTokens.Enqueue(combination);
                    }
                }
            }
        }
        else
        {
            // Simple generic type like List<T>
            // Add just the base type without generics
            pendingTokens.Enqueue(baseType);
        }
    }

    private void GenerateBacktickGenericTokens(string fullName)
    {
        // Find the position of the backtick
        int backtickIndex = fullName.IndexOf('`');
        string baseType = fullName[..backtickIndex];

        // Add the full type with backtick notation
        pendingTokens.Enqueue(fullName);

        // Now process the base type (which might have dots)
        string[] parts = baseType.Split('.');

        // If the base type has dots (namespace), process it
        if (parts.Length > 1)
        {
            // Add individual parts
            foreach (string part in parts)
            {
                if (!string.IsNullOrEmpty(part))
                {
                    pendingTokens.Enqueue(part);
                }
            }

            // Add the last part with backtick suffix
            string lastPart = parts[^1];
            string backtickSuffix = fullName[backtickIndex..];
            pendingTokens.Enqueue(lastPart + backtickSuffix);

            // Add hierarchical combinations
            for (int i = 0; i < parts.Length - 1; i++)
            {
                for (int j = i + 1; j <= parts.Length; j++)
                {
                    string combination = string.Join(".", parts, i, j - i);
                    if (j == parts.Length)
                    {
                        // If it's the full namespace, add with backtick suffix
                        pendingTokens.Enqueue(combination + backtickSuffix);
                    }
                    else if (j - i > 1)
                    {
                        // Otherwise just add the namespace combination
                        pendingTokens.Enqueue(combination);
                    }
                }
            }
        }
        else
        {
            // Simple type like List`1 or ToReadOnly``1
            // Add just the base type without backticks
            pendingTokens.Enqueue(baseType);
        }
    }

    public override void Reset()
    {
        base.Reset();
        pendingTokens.Clear();
    }
}