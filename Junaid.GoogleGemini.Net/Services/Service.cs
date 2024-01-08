using Junaid.GoogleGemini.Net.Infrastructure;
using Microsoft.Extensions.Options;

namespace Junaid.GoogleGemini.Net.Services
{
    public class Service
    {
        protected readonly IGeminiClient GeminiClient;
        protected readonly IOptionsSnapshot<GeminiConfiguration> OptionsSnapshot;

        public Service(IOptionsSnapshot<GeminiConfiguration> optionsSnapshot)
        {
            OptionsSnapshot = optionsSnapshot;
            GeminiClient = new GeminiClient(optionsSnapshot);
        }
    }
}