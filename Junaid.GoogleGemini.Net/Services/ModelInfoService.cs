using Junaid.GoogleGemini.Net.Infrastructure;
using Junaid.GoogleGemini.Net.Services.Interfaces;

namespace Junaid.GoogleGemini.Net.Services
{
    public class ModelInfoService : Service, IModelInfoService
    {
        public ModelInfoService(GeminiClient geminiClient) : base(geminiClient)
        {
        }

        /// <summary>
        ///
        /// </summary>
        /// <returns></returns>
        public async Task<ListModelInfoResponse> ListModelsAsync()
        {
            return await GeminiClient.GetAsync<ListModelInfoResponse>($"/v1beta/models");
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public async Task<ModelInfo> GetModelAsync(string name)
        {
            return await GeminiClient.GetAsync<ModelInfo>($"/v1beta/models/{name}");
        }
    }
}