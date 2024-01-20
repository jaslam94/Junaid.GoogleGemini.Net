namespace Junaid.GoogleGemini.Net.Services.Interfaces
{
    public interface IModelInfoService
    {
        Task<ModelInfo> GetModelAsync(string name);
        Task<ListModelInfoResponse> ListModelsAsync();
    }
}