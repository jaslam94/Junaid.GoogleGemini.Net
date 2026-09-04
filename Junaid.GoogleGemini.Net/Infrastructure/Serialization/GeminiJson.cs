using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Junaid.GoogleGemini.Net.Infrastructure.Serialization;

/// <summary>
/// The single, shared <see cref="JsonSerializerOptions"/> used for all Gemini (de)serialization.
///
/// Having exactly one configured instance, instead of constructing options ad hoc in each class,
/// is what guarantees a consistent wire format across the whole library. (The previous code created
/// options in several places with different settings, which is how serialization drifted.)
///
/// The resolver chain puts the source-generated metadata first (fast, AOT-friendly) and falls back
/// to reflection for anything not pre-registered, for example a caller's own type used with the future
/// structured-output <c>&lt;T&gt;</c> API, or arbitrary function-call results.
/// </summary>
public static class GeminiJson
{
    /// <summary>The canonical options instance. Treat as immutable; do not mutate after first use.</summary>
    public static readonly JsonSerializerOptions Default = CreateDefault();

    private static JsonSerializerOptions CreateDefault()
    {
        var options = new JsonSerializerOptions
        {
            // Omit null members so we never send {"temperature":null} and friends to the API.
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            // Be lenient when reading the API's responses.
            PropertyNameCaseInsensitive = true,
        };

        // Source-generated metadata first, reflection second.
        options.TypeInfoResolverChain.Add(GeminiJsonContext.Default);
        options.TypeInfoResolverChain.Add(new DefaultJsonTypeInfoResolver());

        return options;
    }
}
