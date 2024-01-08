using Junaid.GoogleGemini.Net.Infrastructure;

namespace Junaid.GoogleGemini.Net.Services
{
    /// <summary>
    /// 
    /// </summary>
    public class ModelInfoService
    {
        private readonly IGeminiClient GeminiClient;

        /// <summary>
        /// 
        /// </summary>
        public ModelInfoService()
        {
            GeminiClient = GeminiConfiguration.HttpClient;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="geminiClient"></param>
        public ModelInfoService(IGeminiClient geminiClient)
        {
            GeminiClient = geminiClient;
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