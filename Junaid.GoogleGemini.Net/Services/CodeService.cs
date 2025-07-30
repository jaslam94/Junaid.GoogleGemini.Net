using Junaid.GoogleGemini.Net.Infrastructure.Interfaces;
using Junaid.GoogleGemini.Net.Infrastructure.Options;
using Junaid.GoogleGemini.Net.Infrastructure.Utilities;
using Junaid.GoogleGemini.Net.Models.GoogleApi;
using Junaid.GoogleGemini.Net.Models.Requests;
using Junaid.GoogleGemini.Net.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Junaid.GoogleGemini.Net.Services
{
    /// <summary>
    /// DEPRECATED: Use IGeminiService with GeminiRequestOptions.Code() for code operations. Will be removed in v7.0.0
    /// Service for code-related operations using Gemini API
    /// </summary>
    [Obsolete("Use IGeminiService with GeminiRequestOptions.Code() for code operations. This service will be removed in v7.0.0")]
    public class CodeService : Service, ICodeService
    {
        private const int MAX_CODE_LENGTH = GeminiConstants.Limits.MaxTextLength;
        private const int MAX_PROMPT_LENGTH = GeminiConstants.Limits.MaxMessageLength;
        
        private static readonly string[] SupportedLanguages = 
        {
            "csharp", "c#", "java", "python", "javascript", "typescript", "go", "rust",
            "cpp", "c++", "c", "php", "ruby", "swift", "kotlin", "scala", "html", "css", "sql"
        };

        /// <summary>
        /// Initializes a new instance of the CodeService
        /// </summary>
        public CodeService(
            IGeminiClient geminiClient,
            ILogger<CodeService> logger,
            IOptions<GeminiOptions> options,
            ISafetyService safetyService) : base(geminiClient, logger, options, safetyService)
        {
        }

        /// <inheritdoc/>
        public async Task<GenerateContentResponse> GenerateCodeAsync(
            string prompt,
            string programmingLanguage,
            CancellationToken cancellationToken = default)
        {
            ValidateCodeGenerationInputs(prompt, programmingLanguage);

            var fullPrompt = $"Generate {programmingLanguage} code for: {prompt}\n" +
                           $"Only provide the code without any explanations. " +
                           $"Make sure the code follows best practices and includes proper error handling.";

            var endpoint = $"/models/{GeminiConstants.Models.Recommended}:generateContent";
            var request = Infrastructure.Factories.RequestFactory.CreateTextRequest(fullPrompt, GeminiRequestOptions.Code());

            return await ExecuteRequestAsync<GenerateContentRequest, GenerateContentResponse>(
                "code generation",
                endpoint,
                request,
                new { Language = programmingLanguage, PromptLength = prompt.Length },
                cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<GenerateContentResponse> ReviewCodeAsync(
            string code,
            string programmingLanguage,
            CancellationToken cancellationToken = default)
        {
            ValidateCodeInputs(code, programmingLanguage);

            var prompt = CreateCodeReviewPrompt(code, programmingLanguage);
            var endpoint = $"/models/{GeminiConstants.Models.Recommended}:generateContent";
            var request = Infrastructure.Factories.RequestFactory.CreateTextRequest(prompt, GeminiRequestOptions.Factual());

            return await ExecuteRequestAsync<GenerateContentRequest, GenerateContentResponse>(
                "code review",
                endpoint,
                request,
                new { Language = programmingLanguage, CodeLength = code.Length },
                cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<GenerateContentResponse> ExplainCodeAsync(
            string code,
            string programmingLanguage,
            CancellationToken cancellationToken = default)
        {
            ValidateCodeInputs(code, programmingLanguage);

            var prompt = CreateCodeExplanationPrompt(code, programmingLanguage);
            var endpoint = $"/models/{GeminiConstants.Models.Recommended}:generateContent";
            var request = Infrastructure.Factories.RequestFactory.CreateTextRequest(prompt, GeminiRequestOptions.Factual());

            return await ExecuteRequestAsync<GenerateContentRequest, GenerateContentResponse>(
                "code explanation",
                endpoint,
                request,
                new { Language = programmingLanguage, CodeLength = code.Length },
                cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<GenerateContentResponse> DocumentCodeAsync(
            string code,
            string programmingLanguage,
            CancellationToken cancellationToken = default)
        {
            ValidateCodeInputs(code, programmingLanguage);

            var prompt = CreateCodeDocumentationPrompt(code, programmingLanguage);
            var endpoint = $"/models/{GeminiConstants.Models.Recommended}:generateContent";
            var request = Infrastructure.Factories.RequestFactory.CreateTextRequest(prompt, GeminiRequestOptions.Factual());

            return await ExecuteRequestAsync<GenerateContentRequest, GenerateContentResponse>(
                "code documentation",
                endpoint,
                request,
                new { Language = programmingLanguage, CodeLength = code.Length },
                cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<GenerateContentResponse> TranslateCodeAsync(
            string code,
            string fromLanguage,
            string toLanguage,
            CancellationToken cancellationToken = default)
        {
            ValidateCodeInputs(code, fromLanguage);
            ValidateLanguage(toLanguage, nameof(toLanguage));

            var prompt = CreateCodeTranslationPrompt(code, fromLanguage, toLanguage);
            var endpoint = $"/models/{GeminiConstants.Models.Recommended}:generateContent";
            var request = Infrastructure.Factories.RequestFactory.CreateTextRequest(prompt, GeminiRequestOptions.Code());

            return await ExecuteRequestAsync<GenerateContentRequest, GenerateContentResponse>(
                "code translation",
                endpoint,
                request,
                new { FromLanguage = fromLanguage, ToLanguage = toLanguage, CodeLength = code.Length },
                cancellationToken);
        }

        private void ValidateCodeGenerationInputs(string prompt, string programmingLanguage)
        {
            ValidationUtilities.ValidateTextInput(prompt, nameof(prompt), MAX_PROMPT_LENGTH);
            ValidateLanguage(programmingLanguage, nameof(programmingLanguage));
        }

        private void ValidateCodeInputs(string code, string programmingLanguage)
        {
            ValidationUtilities.ValidateTextInput(code, nameof(code), MAX_CODE_LENGTH);
            ValidateLanguage(programmingLanguage, nameof(programmingLanguage));
        }

        private static void ValidateLanguage(string programmingLanguage, string paramName)
        {
            if (string.IsNullOrWhiteSpace(programmingLanguage))
            {
                throw new ArgumentException("Programming language cannot be null or empty", paramName);
            }

            var normalizedLanguage = programmingLanguage.ToLowerInvariant();
            if (!SupportedLanguages.Contains(normalizedLanguage))
            {
                throw new ArgumentException(
                    $"Unsupported programming language: {programmingLanguage}. " +
                    $"Supported languages: {string.Join(", ", SupportedLanguages)}",
                    paramName);
            }
        }

        private static string CreateCodeReviewPrompt(string code, string programmingLanguage)
        {
            return $"Review this {programmingLanguage} code and suggest improvements:\n```{programmingLanguage}\n{code}\n```\n" +
                   "Focus on:\n" +
                   "1. Code quality and best practices\n" +
                   "2. Performance optimizations\n" +
                   "3. Security considerations\n" +
                   "4. Error handling\n" +
                   "5. Maintainability";
        }

        private static string CreateCodeExplanationPrompt(string code, string programmingLanguage)
        {
            return $"Explain this {programmingLanguage} code in detail:\n```{programmingLanguage}\n{code}\n```\n" +
                   "Include:\n" +
                   "1. Overall purpose\n" +
                   "2. How it works\n" +
                   "3. Key components and their roles\n" +
                   "4. Any important patterns or techniques used";
        }

        private static string CreateCodeDocumentationPrompt(string code, string programmingLanguage)
        {
            return $"Add comprehensive documentation to this {programmingLanguage} code:\n```{programmingLanguage}\n{code}\n```\n" +
                   "Include:\n" +
                   "1. File/class/module level documentation\n" +
                   "2. Function/method documentation with parameters and return values\n" +
                   "3. Important code block explanations\n" +
                   "4. Usage examples where appropriate\n" +
                   "Return the fully documented code.";
        }

        private static string CreateCodeTranslationPrompt(string code, string fromLanguage, string toLanguage)
        {
            return $"Convert this {fromLanguage} code to {toLanguage}:\n```{fromLanguage}\n{code}\n```\n" +
                   "Ensure the converted code:\n" +
                   "1. Maintains the same functionality\n" +
                   $"2. Follows {toLanguage} best practices\n" +
                   $"3. Uses idiomatic patterns for {toLanguage}\n" +
                   "Only provide the converted code without explanations.";
        }
    }
}
