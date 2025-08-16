using System.Text.Json;
using System.Text.Json.Serialization;

namespace ApiLens.Cli.Services;

/// <summary>
/// Custom JSON converter that sanitizes strings during serialization.
/// </summary>
public class SanitizingStringConverter : JsonConverter<string>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.GetString();
    }

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
        }
        else
        {
            // Sanitize the string before writing
            string sanitized = JsonSanitizer.SanitizeForJson(value) ?? string.Empty;
            writer.WriteStringValue(sanitized);
        }
    }
}

/// <summary>
/// Factory for creating converters that handle all types with string properties.
/// </summary>
public class SanitizingJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
    {
        // Since we're now sanitizing at the source (in QueryEngine),
        // we don't need this converter factory to do anything
        return false;
    }

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        throw new NotSupportedException("This converter factory is not used anymore");
    }

    private class ObjectWithStringsConverter : JsonConverter<object>
    {
        public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            throw new NotImplementedException("Deserialization is not supported");
        }

        public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNullValue();
                return;
            }

            writer.WriteStartObject();

            var type = value.GetType();
            foreach (var property in type.GetProperties())
            {
                if (!property.CanRead)
                    continue;

                var propertyValue = property.GetValue(value);

                // Convert property name to camelCase if needed
                string propertyName = options.PropertyNamingPolicy?.ConvertName(property.Name) ?? property.Name;

                if (propertyValue == null)
                {
                    writer.WriteNull(propertyName);
                }
                else if (property.PropertyType == typeof(string))
                {
                    // Sanitize string values
                    string sanitized = JsonSanitizer.SanitizeForJson((string)propertyValue) ?? string.Empty;
                    writer.WriteString(propertyName, sanitized);
                }
                else if (property.PropertyType == typeof(DateTime) || property.PropertyType == typeof(DateTime?))
                {
                    // Handle DateTime properly
                    writer.WritePropertyName(propertyName);
                    JsonSerializer.Serialize(writer, propertyValue, property.PropertyType, options);
                }
                else if (IsCollection(property.PropertyType))
                {
                    // Handle collections with potential string elements
                    writer.WritePropertyName(propertyName);
                    WriteCollection(writer, propertyValue, options);
                }
                else
                {
                    // For other properties, use default serialization with sanitization
                    writer.WritePropertyName(propertyName);

                    // Create new options with our converter to handle nested objects
                    var newOptions = new JsonSerializerOptions(options);
                    if (!newOptions.Converters.Any(c => c is SanitizingJsonConverterFactory))
                    {
                        newOptions.Converters.Add(new SanitizingJsonConverterFactory());
                    }

                    JsonSerializer.Serialize(writer, propertyValue, property.PropertyType, newOptions);
                }
            }

            writer.WriteEndObject();
        }

        private bool IsCollection(Type type)
        {
            if (type.IsArray) return true;

            // Check for IEnumerable but not string
            if (type != typeof(string) && type.GetInterfaces().Any(i =>
                i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>)))
            {
                return true;
            }

            // Check for ImmutableArray
            if (type.IsGenericType && type.Name.StartsWith("ImmutableArray"))
            {
                return true;
            }

            return false;
        }

        private void WriteCollection(Utf8JsonWriter writer, object collection, JsonSerializerOptions options)
        {
            writer.WriteStartArray();

            if (collection is System.Collections.IEnumerable enumerable)
            {
                foreach (var item in enumerable)
                {
                    if (item == null)
                    {
                        writer.WriteNullValue();
                    }
                    else if (item is string str)
                    {
                        // Sanitize string items in collections
                        string sanitized = JsonSanitizer.SanitizeForJson(str) ?? string.Empty;
                        writer.WriteStringValue(sanitized);
                    }
                    else
                    {
                        // Recursively handle nested objects
                        var newOptions = new JsonSerializerOptions(options);
                        if (!newOptions.Converters.Any(c => c is SanitizingJsonConverterFactory))
                        {
                            newOptions.Converters.Add(new SanitizingJsonConverterFactory());
                        }
                        JsonSerializer.Serialize(writer, item, item.GetType(), newOptions);
                    }
                }
            }

            writer.WriteEndArray();
        }
    }
}