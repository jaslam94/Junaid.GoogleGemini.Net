namespace Junaid.GoogleGemini.Net.Infrastructure
{
    public interface IGeminiAuthHttpClientOptions
    {
        Uri Url { get; }
        GeminiConfiguration? Credentials { get; set; }
    }

    public class GeminiHttpClientOptions : IGeminiAuthHttpClientOptions
    {
        private const string DefaultBaseAddress = "https://generativelanguage.googleapis.com";

        private readonly Uri uri = new Uri(DefaultBaseAddress);

        public Uri Url
        {
            get
            {
                return uri;
            }
        }

        public GeminiConfiguration? Credentials { get; set; }
    }
}