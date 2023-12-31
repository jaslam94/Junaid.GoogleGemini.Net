namespace Junaid.GoogleGemini.Net.Models.GoogleApi
{
    public class BatchEmbedContentRequest
    {
        public EmbedContentRequest[] requests { get; set; }
    }

    public class EmbedContentRequest
    {
        public string model { get; set; }
        public Content content { get; set; }
    }
}