namespace Junaid.GoogleGemini.Net.Infrastructure
{
    public interface IGeminiAuthHttpClientOptions
    {
        Uri Url { get; }
        GeminiConfiguration Credentials { get; set; }
    }

    public class GeminiHttpClientOptions : IGeminiAuthHttpClientOptions
    {
        private const string DefaultBaseAddress = "https://generativelanguage.googleapis.com";

        private readonly Uri url = new Uri(DefaultBaseAddress);

        private GeminiConfiguration? credentials;

        public Uri Url
        {
            get
            {
                return url;
            }
        }

        public GeminiConfiguration Credentials
        {
            get
            {
                if (credentials == null)
                {
                    throw new ArgumentNullException(nameof(Credentials));
                }
                return credentials;
            }

            set
            {
                credentials = value ?? throw new ArgumentNullException(nameof(Credentials));
            }
        }
    }
}