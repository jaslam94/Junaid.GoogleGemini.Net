using Junaid.GoogleGemini.Net.Infrastructure;
using Junaid.GoogleGemini.Net.Infrastructure.Interfaces;

namespace Junaid.GoogleGemini.Net.Services
{
    public abstract class Service
    {
        protected readonly IGeminiClient GeminiClient;

        protected Service(IGeminiClient geminiClient)
        {
            GeminiClient = geminiClient ?? throw new ArgumentNullException(nameof(geminiClient));
        }
    }
}