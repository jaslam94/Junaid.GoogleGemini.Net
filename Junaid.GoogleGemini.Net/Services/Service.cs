using Junaid.GoogleGemini.Net.Infrastructure;
using Microsoft.Extensions.Options;

namespace Junaid.GoogleGemini.Net.Services
{
    public class Service
    {
        protected readonly IGeminiClient GeminiClient;

        public Service(IGeminiClient geminiClient)
        {
            GeminiClient = geminiClient;
        }
    }
}