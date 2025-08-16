using System.Text.RegularExpressions;

namespace ApiLens.Cli.Services;

/// <summary>
/// Provides methods to sanitize strings for safe JSON serialization.
/// Handles control characters and ensures proper escaping.
/// </summary>
public static partial class JsonSanitizer
{
    // Regex to match control characters (U+0000 through U+001F)
    [GeneratedRegex(@"[\x00-\x1F]", RegexOptions.Compiled)]
    private static partial Regex ControlCharacterRegex();

    /// <summary>
    /// Sanitizes a string by replacing control characters with their escaped equivalents.
    /// </summary>
    /// <param name="input">The string to sanitize</param>
    /// <returns>A sanitized string safe for JSON serialization</returns>
    public static string? SanitizeForJson(string? input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        // Replace control characters with their escaped sequences
        return ControlCharacterRegex().Replace(input, match =>
        {
            char c = match.Value[0];
            return c switch
            {
                '\n' => "\\n",
                '\r' => "\\r",
                '\t' => "\\t",
                '\b' => "\\b",
                '\f' => "\\f",
                _ => $"\\u{(int)c:X4}"
            };
        });
    }

    /// <summary>
    /// Recursively sanitizes all string properties in an object.
    /// </summary>
    /// <typeparam name="T">The type of object to sanitize</typeparam>
    /// <param name="obj">The object to sanitize</param>
    /// <returns>The object with all string properties sanitized</returns>
    public static T SanitizeObject<T>(T obj) where T : class
    {
        if (obj == null)
        {
            return obj!;
        }

        Type type = obj.GetType();

        // Handle collections
        if (obj is System.Collections.IEnumerable enumerable && obj is not string)
        {
            foreach (var item in enumerable)
            {
                if (item != null)
                {
                    SanitizeObject(item);
                }
            }
            return obj;
        }

        // Process all properties
        foreach (var property in type.GetProperties())
        {
            if (!property.CanRead)
                continue;

            var value = property.GetValue(obj);
            if (value == null)
                continue;

            // Handle string properties
            if (property.PropertyType == typeof(string))
            {
                if (property.CanWrite)
                {
                    property.SetValue(obj, SanitizeForJson((string)value));
                }
                else
                {
                    // For readonly properties (like in records), we can't modify them
                    // The sanitization needs to happen at serialization time
                }
            }
            // Handle collections and arrays
            else if (value is System.Collections.IEnumerable innerEnumerable && property.PropertyType != typeof(string))
            {
                foreach (var item in innerEnumerable)
                {
                    if (item != null && item.GetType().IsClass && item is not string)
                    {
                        SanitizeObject(item);
                    }
                }
            }
            // Handle nested objects
            else if (property.PropertyType.IsClass)
            {
                SanitizeObject(value);
            }
        }

        return obj;
    }

    /// <summary>
    /// Creates a custom JSON encoder that properly handles control characters.
    /// </summary>
    /// <returns>A configured JavaScriptEncoder instance</returns>
    public static System.Text.Encodings.Web.JavaScriptEncoder CreateSafeJsonEncoder()
    {
        // Create an encoder that allows most characters but properly escapes control characters
        return System.Text.Encodings.Web.JavaScriptEncoder.Create(
            System.Text.Unicode.UnicodeRanges.BasicLatin,
            System.Text.Unicode.UnicodeRanges.Latin1Supplement,
            System.Text.Unicode.UnicodeRanges.LatinExtendedA,
            System.Text.Unicode.UnicodeRanges.LatinExtendedB,
            System.Text.Unicode.UnicodeRanges.GeneralPunctuation,
            System.Text.Unicode.UnicodeRanges.CurrencySymbols,
            System.Text.Unicode.UnicodeRanges.LetterlikeSymbols,
            System.Text.Unicode.UnicodeRanges.NumberForms,
            System.Text.Unicode.UnicodeRanges.Arrows,
            System.Text.Unicode.UnicodeRanges.MathematicalOperators,
            System.Text.Unicode.UnicodeRanges.MiscellaneousTechnical,
            System.Text.Unicode.UnicodeRanges.BoxDrawing,
            System.Text.Unicode.UnicodeRanges.BlockElements,
            System.Text.Unicode.UnicodeRanges.GeometricShapes,
            System.Text.Unicode.UnicodeRanges.MiscellaneousSymbols);
    }
}