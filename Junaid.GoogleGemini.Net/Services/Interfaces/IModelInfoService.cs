using Junaid.GoogleGemini.Net.Models.GoogleApi;
using System.Threading;

namespace Junaid.GoogleGemini.Net.Services.Interfaces
{
    /// <summary>
    /// Interface for retrieving model information from Gemini API
    /// </summary>
    public interface IModelInfoService
    {
        /// <summary>
        /// Lists all available models from the Gemini API
        /// </summary>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>Response containing list of available models</returns>
        Task<ListModelsResponse> ListModelsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets detailed information about a specific model
        /// </summary>
        /// <param name="modelName">Name of the model to get information for</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>Detailed model information</returns>
        /// <exception cref="ArgumentException">Thrown when model name is invalid</exception>
        /// <exception cref="InvalidOperationException">Thrown when model information is not found</exception>
        Task<ModelInfo> GetModelAsync(string modelName, CancellationToken cancellationToken = default);
    }
}