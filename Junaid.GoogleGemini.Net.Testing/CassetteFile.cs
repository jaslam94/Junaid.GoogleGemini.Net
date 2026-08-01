namespace Junaid.GoogleGemini.Net.Testing;

/// <summary>The on-disk shape of a cassette file: an ordered list of HTTP exchanges.</summary>
internal sealed class CassetteFile
{
    public int Version { get; set; } = 1;

    public List<CassetteInteraction> Interactions { get; set; } = [];
}

internal sealed class CassetteInteraction
{
    public CassetteRequest Request { get; set; } = new();

    public CassetteResponse Response { get; set; } = new();
}

/// <summary>
/// Never includes auth headers by construction: <see cref="CassetteHandler"/> is registered as the
/// outermost handler on the Gemini pipeline, so it observes the request before
/// <c>GeminiAuthHandler</c> attaches the API key.
/// </summary>
internal sealed class CassetteRequest
{
    public string Method { get; set; } = "";

    public string Uri { get; set; } = "";

    public string? Body { get; set; }
}

internal sealed class CassetteResponse
{
    public int StatusCode { get; set; }

    public string? ContentType { get; set; }

    public string Body { get; set; } = "";
}
