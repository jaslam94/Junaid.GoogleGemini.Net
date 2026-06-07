using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Junaid.GoogleGemini.Net.Infrastructure.Serialization;

/// <summary>
/// Generates a Gemini-compatible response schema (an OpenAPI-subset JSON Schema) from a CLR type.
/// This powers <c>GenerateAsync&lt;T&gt;</c>: the type becomes the <c>responseSchema</c> so the model
/// returns JSON shaped exactly like the requested type.
/// </summary>
/// <remarks>
/// Reflection-based for portability across net8.0/net9.0. A source-generated, fully AOT-safe variant
/// is a future enhancement (see ROADMAP). Dictionaries are not modeled (treated as objects).
/// </remarks>
internal static class JsonSchemaGenerator
{
    /// <summary>Builds the schema node for <paramref name="type"/>.</summary>
    public static JsonNode Generate(Type type) => Build(type, new HashSet<Type>());

    private static JsonNode Build(Type type, HashSet<Type> visited)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;

        if (type == typeof(string) || type == typeof(char) || type == typeof(Guid)
            || type == typeof(DateTime) || type == typeof(DateTimeOffset) || type == typeof(TimeSpan))
        {
            return new JsonObject { ["type"] = "string" };
        }

        if (type == typeof(bool))
        {
            return new JsonObject { ["type"] = "boolean" };
        }

        if (type == typeof(byte) || type == typeof(sbyte) || type == typeof(short) || type == typeof(ushort)
            || type == typeof(int) || type == typeof(uint) || type == typeof(long) || type == typeof(ulong))
        {
            return new JsonObject { ["type"] = "integer" };
        }

        if (type == typeof(float) || type == typeof(double) || type == typeof(decimal))
        {
            return new JsonObject { ["type"] = "number" };
        }

        if (type.IsEnum)
        {
            var values = new JsonArray();
            foreach (var name in Enum.GetNames(type))
            {
                values.Add(name);
            }
            return new JsonObject { ["type"] = "string", ["enum"] = values };
        }

        var elementType = GetEnumerableElementType(type);
        if (elementType is not null)
        {
            return new JsonObject { ["type"] = "array", ["items"] = Build(elementType, visited) };
        }

        // Complex object.
        var schema = new JsonObject { ["type"] = "object" };

        // Guard against cycles (e.g. a tree node referencing itself).
        if (!visited.Add(type))
        {
            return schema;
        }

        var properties = new JsonObject();
        var required = new JsonArray();
#if NET8_0_OR_GREATER
        var nullability = new NullabilityInfoContext();
#endif

        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!prop.CanRead || prop.GetIndexParameters().Length > 0) continue;
            if (prop.GetCustomAttribute<JsonIgnoreAttribute>() is not null) continue;

            var name = prop.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? prop.Name;
            properties[name] = Build(prop.PropertyType, visited);

            // A property is required unless it's a Nullable<T> value type, or (net8+) a nullable
            // reference type. netstandard2.0 lacks NullabilityInfoContext, so we can only detect the
            // Nullable<T> case there — a conservative, still-valid schema.
            var isNullable = Nullable.GetUnderlyingType(prop.PropertyType) is not null;
#if NET8_0_OR_GREATER
            isNullable = isNullable || nullability.Create(prop).WriteState == NullabilityState.Nullable;
#endif
            if (!isNullable)
            {
                required.Add(name);
            }
        }

        visited.Remove(type);

        schema["properties"] = properties;
        if (required.Count > 0)
        {
            schema["required"] = required;
        }
        return schema;
    }

    private static Type? GetEnumerableElementType(Type type)
    {
        if (type.IsArray)
        {
            return type.GetElementType();
        }

        foreach (var iface in type.GetInterfaces())
        {
            if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            {
                return iface.GetGenericArguments()[0];
            }
        }
        return null;
    }
}
