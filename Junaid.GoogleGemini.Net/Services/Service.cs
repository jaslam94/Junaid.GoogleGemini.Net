using Junaid.GoogleGemini.Net.Infrastructure;

namespace Junaid.GoogleGemini.Net.Services
{
    public class Service
    {
        protected readonly GeminiClient GeminiClient;

        public Service(GeminiClient geminiClient)
        {
            GeminiClient = geminiClient;
        }
    }
}