namespace Junaid.GoogleGemini.Net.Infrastructure.Interfaces
{
    /// <summary>
    /// Legacy configuration interface - DEPRECATED
    /// Use IOptions&lt;GeminiOptions&gt; pattern instead
    /// </summary>
    [Obsolete("This interface is deprecated. Use IOptions<GeminiOptions> pattern instead. Will be removed in v7.0.0")]
    public interface IGeminiConfiguration
    {
        /// <summary>
        /// DEPRECATED: Use GeminiOptions.ApiKey instead
        /// </summary>
        [Obsolete("Use GeminiOptions.ApiKey instead")]
        string ApiKey { get; set; }
    }
}
