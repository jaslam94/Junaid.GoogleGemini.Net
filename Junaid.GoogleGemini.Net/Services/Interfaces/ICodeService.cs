using Junaid.GoogleGemini.Net.Models.GoogleApi;

namespace Junaid.GoogleGemini.Net.Services.Interfaces
{
    /// <summary>
    /// Interface for code-related operations using Gemini
    /// </summary>
    public interface ICodeService
    {
        /// <summary>
        /// Generates code based on the given prompt
        /// </summary>
        /// <param name="prompt">The prompt describing the code to generate</param>
        /// <param name="programmingLanguage">The target programming language</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        Task<GenerateContentResponse> GenerateCodeAsync(string prompt, string programmingLanguage, CancellationToken cancellationToken = default);

        /// <summary>
        /// Reviews and suggests improvements for the given code
        /// </summary>
        /// <param name="code">The code to review</param>
        /// <param name="programmingLanguage">The programming language of the code</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        Task<GenerateContentResponse> ReviewCodeAsync(string code, string programmingLanguage, CancellationToken cancellationToken = default);

        /// <summary>
        /// Explains the given code in detail
        /// </summary>
        /// <param name="code">The code to explain</param>
        /// <param name="programmingLanguage">The programming language of the code</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        Task<GenerateContentResponse> ExplainCodeAsync(string code, string programmingLanguage, CancellationToken cancellationToken = default);

        /// <summary>
        /// Adds documentation to the given code
        /// </summary>
        /// <param name="code">The code to document</param>
        /// <param name="programmingLanguage">The programming language of the code</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        Task<GenerateContentResponse> DocumentCodeAsync(string code, string programmingLanguage, CancellationToken cancellationToken = default);

        /// <summary>
        /// Converts code from one programming language to another
        /// </summary>
        /// <param name="code">The code to translate</param>
        /// <param name="fromLanguage">The source programming language</param>
        /// <param name="toLanguage">The target programming language</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        Task<GenerateContentResponse> TranslateCodeAsync(string code, string fromLanguage, string toLanguage, CancellationToken cancellationToken = default);
    }
}