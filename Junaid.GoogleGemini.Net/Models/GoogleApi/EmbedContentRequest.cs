namespace Junaid.GoogleGemini.Net.Models.GoogleApi;

public class BatchEmbedContentRequest
{
    public EmbedContentRequest[] requests { get; set; }
}

public class EmbedContentRequest
{
    public string model { get; set; }
    public Content content { get; set; }
}

/// <summary>
/// Request for single embedding generation (simpler structure for direct embedContent API)
/// </summary>
public class SingleEmbedContentRequest
{
    public Content content { get; set; }
}