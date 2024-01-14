namespace Junaid.GoogleGemini.Net.Infrastructure
{
    public interface IGeminiAuthHttpClientOptions
    {
        Uri Url { get; set; }
        GeminiConfiguration Credentials { get; set; }
    }

    public class GeminiHttpClientOptions : IGeminiAuthHttpClientOptions
    {
        private Uri? url;

        private GeminiConfiguration? credentials;

        public Uri Url
        {
            get
            {
                if (url == null)
                {
                    throw new ArgumentNullException(nameof(Url));
                }
                return url;
            }

            set
            {
                url = value ?? throw new ArgumentNullException(nameof(Url));
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