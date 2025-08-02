namespace ApiLens.Core.Formatting;

/// <summary>
/// Formats generic type names from XML notation to user-friendly display format.
/// </summary>
public static class GenericTypeFormatter
{
    /// <summary>
    /// Formats a type name with backtick notation to angle bracket notation.
    /// </summary>
    /// <param name="typeName">The type name to format.</param>
    /// <returns>The formatted type name.</returns>
    public static string FormatTypeName(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
            return typeName;

        // Handle double backtick (generic methods)
        if (typeName.Contains("``"))
        {
            int doubleBacktickIndex = typeName.IndexOf("``", StringComparison.Ordinal);
            string baseName = typeName[..doubleBacktickIndex];
            string paramCount = typeName[(doubleBacktickIndex + 2)..];

            if (int.TryParse(paramCount, out int count))
            {
                string typeParams = GenerateTypeParameters(count);
                return $"{baseName}<{typeParams}>";
            }
        }

        // Handle single backtick (generic types)
        if (typeName.Contains('`'))
        {
            int backtickIndex = typeName.IndexOf('`', StringComparison.Ordinal);
            string baseName = typeName[..backtickIndex];
            string remaining = typeName[(backtickIndex + 1)..];

            // Extract the number of parameters
            int endIndex = 0;
            while (endIndex < remaining.Length && char.IsDigit(remaining[endIndex]))
            {
                endIndex++;
            }

            if (endIndex > 0 && int.TryParse(remaining[..endIndex], out int count))
            {
                string typeParams = GenerateTypeParameters(count);
                string suffix = remaining[endIndex..]; // Any additional text after the number
                return $"{baseName}<{typeParams}>{suffix}";
            }
        }

        return typeName;
    }

    /// <summary>
    /// Formats a full member name including namespace and type parameters.
    /// </summary>
    /// <param name="fullName">The full member name to format.</param>
    /// <returns>The formatted full name.</returns>
    public static string FormatFullName(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return fullName;
        }

        // For method signatures, we need to handle the parameter list separately
        int parenIndex = fullName.IndexOf('(');
        if (parenIndex > 0)
        {
            string beforeParen = fullName[..parenIndex];
            string afterParen = fullName[parenIndex..];

            // Format the method name part
            string formattedBefore = FormatTypeNameInNamespace(beforeParen);

            // Format types in the parameter list
            string formattedParams = FormatParameterList(afterParen);

            return formattedBefore + formattedParams;
        }

        return FormatTypeNameInNamespace(fullName);
    }

    private static string FormatTypeNameInNamespace(string fullName)
    {
        // Split by dots to handle namespace
        string[] parts = fullName.Split('.');
        if (parts.Length == 0)
            return fullName;

        // Format the last part (which might be a generic type)
        parts[^1] = FormatTypeName(parts[^1]);

        return string.Join(".", parts);
    }

    private static string FormatParameterList(string paramList)
    {
        // This is a simplified version - a full implementation would need to
        // parse the parameter types and format each one
        return paramList;
    }

    private static string GenerateTypeParameters(int count)
    {
        return count switch
        {
            1 => "T",
            2 => "TKey,TValue",
            3 => "T1,T2,T3",
            4 => "T1,T2,T3,T4",
            _ => string.Join(",", Enumerable.Range(1, count).Select(i => $"T{i}"))
        };
    }
}