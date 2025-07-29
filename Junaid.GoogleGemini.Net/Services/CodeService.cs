using Junaid.GoogleGemini.Net.Infrastructure.Extensions;
using Junaid.GoogleGemini.Net.Infrastructure.Interfaces;
using Junaid.GoogleGemini.Net.Infrastructure.Options;
using Junaid.GoogleGemini.Net.Models.GoogleApi;
using Junaid.GoogleGemini.Net.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Junaid.GoogleGemini.Net.Services
{
    /// <summary>
    /// Service for code-related operations using Gemini API
    /// </summary>
    public class CodeService : Service, ICodeService
    {
        private const string MODEL_NAME = "gemini-pro";
        private const int MAX_CODE_LENGTH = 50000;
        private const int MAX_PROMPT_LENGTH = 5000;
        
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

            try
            {
                LogOperationStart("code generation", new 
                { 
                    Language = programmingLanguage, 
                    PromptLength = prompt.Length 
                });

                var request = GeminiClient.CreateCodeRequest(prompt, programmingLanguage).Build();
                var endpoint = $"/models/{MODEL_NAME}:generateContent";
                
                var response = await GeminiClient.PostAsync<GenerateContentRequest, GenerateContentResponse>(
                    endpoint,
                    request,
                    cancellationToken);

                ValidateResponse(response, "code generation");
                LogOperationSuccess("code generation");
                
                return response;
            }
            catch (Exception ex) when (ex is not (ArgumentException or InvalidOperationException))
            {
                LogOperationError(ex, "code generation");
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<GenerateContentResponse> ReviewCodeAsync(
            string code,
            string programmingLanguage,
            CancellationToken cancellationToken = default)
        {
            ValidateCodeInputs(code, programmingLanguage);

            try
            {
                LogOperationStart("code review", new 
                { 
                    Language = programmingLanguage, 
                    CodeLength = code.Length 
                });

                var prompt = CreateCodeReviewPrompt(code, programmingLanguage);
                var request = GeminiClient.CreateFactualRequest(prompt).Build();
                var endpoint = $"/models/{MODEL_NAME}:generateContent";
                
                var response = await GeminiClient.PostAsync<GenerateContentRequest, GenerateContentResponse>(
                    endpoint,
                    request,
                    cancellationToken);

                ValidateResponse(response, "code review");
                LogOperationSuccess("code review");
                
                return response;
            }
            catch (Exception ex) when (ex is not (ArgumentException or InvalidOperationException))
            {
                LogOperationError(ex, "code review");
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<GenerateContentResponse> ExplainCodeAsync(
            string code,
            string programmingLanguage,
            CancellationToken cancellationToken = default)
        {
            ValidateCodeInputs(code, programmingLanguage);

            try
            {
                LogOperationStart("code explanation", new 
                { 
                    Language = programmingLanguage, 
                    CodeLength = code.Length 
                });

                var prompt = CreateCodeExplanationPrompt(code, programmingLanguage);
                var request = GeminiClient.CreateFactualRequest(prompt).Build();
                var endpoint = $"/models/{MODEL_NAME}:generateContent";
                
                var response = await GeminiClient.PostAsync<GenerateContentRequest, GenerateContentResponse>(
                    endpoint,
                    request,
                    cancellationToken);

                ValidateResponse(response, "code explanation");
                LogOperationSuccess("code explanation");
                
                return response;
            }
            catch (Exception ex) when (ex is not (ArgumentException or InvalidOperationException))
            {
                LogOperationError(ex, "code explanation");
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<GenerateContentResponse> DocumentCodeAsync(
            string code,
            string programmingLanguage,
            CancellationToken cancellationToken = default)
        {
            ValidateCodeInputs(code, programmingLanguage);

            try
            {
                LogOperationStart("code documentation", new 
                { 
                    Language = programmingLanguage, 
                    CodeLength = code.Length 
                });

                var prompt = CreateCodeDocumentationPrompt(code, programmingLanguage);
                var request = GeminiClient.CreateFactualRequest(prompt).Build();
                var endpoint = $"/models/{MODEL_NAME}:generateContent";
                
                var response = await GeminiClient.PostAsync<GenerateContentRequest, GenerateContentResponse>(
                    endpoint,
                    request,
                    cancellationToken);

                ValidateResponse(response, "code documentation");
                LogOperationSuccess("code documentation");
                
                return response;
            }
            catch (Exception ex) when (ex is not (ArgumentException or InvalidOperationException))
            {
                LogOperationError(ex, "code documentation");
                throw;
            }
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

            try
            {
                LogOperationStart("code translation", new 
                { 
                    FromLanguage = fromLanguage, 
                    ToLanguage = toLanguage, 
                    CodeLength = code.Length 
                });

                var prompt = CreateCodeTranslationPrompt(code, fromLanguage, toLanguage);
                var request = GeminiClient.CreateCodeRequest(prompt, toLanguage).Build();
                var endpoint = $"/models/{MODEL_NAME}:generateContent";
                
                var response = await GeminiClient.PostAsync<GenerateContentRequest, GenerateContentResponse>(
                    endpoint,
                    request,
                    cancellationToken);

                ValidateResponse(response, "code translation");
                LogOperationSuccess("code translation");
                
                return response;
            }
            catch (Exception ex) when (ex is not (ArgumentException or InvalidOperationException))
            {
                LogOperationError(ex, "code translation");
                throw;
            }
        }

        private void ValidateCodeGenerationInputs(string prompt, string programmingLanguage)
        {
            ValidateTextInput(prompt, nameof(prompt), MAX_PROMPT_LENGTH);
            ValidateLanguage(programmingLanguage, nameof(programmingLanguage));
        }

        private void ValidateCodeInputs(string code, string programmingLanguage)
        {
            ValidateTextInput(code, nameof(code), MAX_CODE_LENGTH);
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
