﻿namespace Junaid.GoogleGemini.Net.Infrastructure
{
    public interface IGeminiClient
    {
        Task<TResponse> GetAsync<TResponse>(string endpoint);

        Task<TResponse> PostAsync<TRequest, TResponse>(string endpoint, TRequest data);

        IAsyncEnumerable<string> PostAsync<TRequest>(string endpoint, TRequest data);
    }
}