namespace Junaid.GoogleGemini.Net.Models.GoogleApi
{
    public class EmbedContentResponse
    {
        public Embedding embedding { get; set; }
    }

    public class Embedding
    {
        public float[] values { get; set; }
    }
}