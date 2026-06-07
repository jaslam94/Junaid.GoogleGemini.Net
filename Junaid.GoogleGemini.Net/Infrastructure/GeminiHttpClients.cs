namespace Junaid.GoogleGemini.Net.Infrastructure
{
    /// <summary>Named <see cref="HttpClient"/> keys used by the library's DI registration.</summary>
    public static class GeminiHttpClients
    {
        /// <summary>
        /// Client for the Files API. It targets the host root (not the versioned base address) because
        /// uploads use the <c>/upload</c> path and a server-issued absolute session URL.
        /// </summary>
        public const string Files = "GeminiFiles";
    }
}
