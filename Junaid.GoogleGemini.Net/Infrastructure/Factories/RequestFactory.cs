using Junaid.GoogleGemini.Net.Infrastructure.Utilities;
using Junaid.GoogleGemini.Net.Models.GoogleApi;
using Junaid.GoogleGemini.Net.Models.Requests;

namespace Junaid.GoogleGemini.Net.Infrastructure.Factories
{
    /// <summary>
    /// Factory for creating simple Gemini API requests without builder pattern overhead
    /// </summary>
    public static class RequestFactory
    {
        /// <summary>
        /// Creates a simple text request
        /// </summary>
        public static GenerateContentRequest CreateTextRequest(string text, GeminiRequestOptions? options = null)
        {
            var content = new Content
            {
                Role = "user",
                Parts = new List<Part> { new() { Text = text } }
            };

            return new GenerateContentRequest
            {
                Contents = new List<Content> { content },
                GenerationConfig = CreateGenerationConfig(options),
                SystemInstruction = BuildSystemInstruction(options),
                Tools = BuildTools(options),
                ToolConfig = options?.ToolConfig,
                CachedContent = options?.CachedContent,
                SafetySettings = options?.SafetySettings ?? GetDefaultSafetySettings()
            };
        }

        /// <summary>
        /// Creates a simple vision request (text + image)
        /// </summary>
        public static GenerateContentRequest CreateVisionRequest(
            string text, 
            FileObject image, 
            GeminiRequestOptions? options = null,
            bool streaming = false)
        {
            var parts = new List<Part>
            {
                new() { Text = text },
                new() 
                { 
                    InlineData = new InlineData
                    {
                        Data = Convert.ToBase64String(image.FileContent),
                        MimeType = FileUtilities.GetMimeType(image.FileName)
                    }
                }
            };

            var content = new Content
            {
                Role = "user",
                Parts = parts
            };

            return new GenerateContentRequest
            {
                Contents = new List<Content> { content },
                GenerationConfig = CreateGenerationConfig(options),
                SystemInstruction = BuildSystemInstruction(options),
                Tools = BuildTools(options),
                ToolConfig = options?.ToolConfig,
                CachedContent = options?.CachedContent,
                SafetySettings = options?.SafetySettings ?? ConfigurationUtilities.CreateSafetySettings(ConfigurationUtilities.GetDefaultSafetyThresholds())
            };
        }

        /// <summary>
        /// Creates a simple chat request from message history
        /// </summary>
        public static GenerateContentRequest CreateChatRequest(
            MessageObject[] messages, 
            GeminiRequestOptions? options = null)
        {
            var contents = messages.Select(msg => new Content
            {
                Role = msg.Role,
                Parts = new List<Part> { new() { Text = msg.Text } }
            }).ToList();

            return new GenerateContentRequest
            {
                Contents = contents,
                GenerationConfig = CreateGenerationConfig(options),
                SystemInstruction = BuildSystemInstruction(options),
                Tools = BuildTools(options),
                ToolConfig = options?.ToolConfig,
                CachedContent = options?.CachedContent,
                SafetySettings = options?.SafetySettings ?? GetDefaultSafetySettings()
            };
        }

        /// <summary>
        /// Builds a request directly from a list of <see cref="Content"/> items. Use this for full
        /// multi-turn control. It echoes back response parts (function calls, function responses, and
        /// Gemini 3 thought signatures) that the simpler text-based chat helpers can't represent.
        /// </summary>
        public static GenerateContentRequest CreateRequest(IEnumerable<Content> contents, GeminiRequestOptions? options = null)
        {
            return new GenerateContentRequest
            {
                Contents = contents.ToList(),
                GenerationConfig = CreateGenerationConfig(options),
                SystemInstruction = BuildSystemInstruction(options),
                Tools = BuildTools(options),
                ToolConfig = options?.ToolConfig,
                CachedContent = options?.CachedContent,
                SafetySettings = options?.SafetySettings ?? GetDefaultSafetySettings()
            };
        }

        /// <summary>
        /// Creates generation config from options
        /// </summary>
        private static GenerationConfig CreateGenerationConfig(GeminiRequestOptions? options)
        {
            // No options: send an empty config so the model uses its native defaults (notably
            // temperature 1.0 on Gemini 3, which advises against forcing a lower value).
            if (options == null)
            {
                return new GenerationConfig();
            }

            return new GenerationConfig
            {
                Temperature = options.Temperature,
                MaxOutputTokens = options.MaxTokens,
                TopP = options.TopP,
                TopK = options.TopK,
                StopSequences = options.StopSequences,
                CandidateCount = options.CandidateCount,
                Seed = options.Seed,
                ResponseMimeType = options.ResponseMimeType,
                ResponseSchema = options.ResponseSchema,
                MediaResolution = options.MediaResolution,
                ThinkingConfig = BuildThinkingConfig(options),
                ResponseModalities = options.ResponseModalities,
                ImageConfig = BuildImageConfig(options)
            };
        }

        /// <summary>Builds an image config from options, or null when neither field is set.</summary>
        private static ImageConfig? BuildImageConfig(GeminiRequestOptions options)
        {
            if (options.ImageAspectRatio is null && options.ImageSize is null)
            {
                return null;
            }

            return new ImageConfig
            {
                AspectRatio = options.ImageAspectRatio,
                ImageSize = options.ImageSize
            };
        }

        /// <summary>Builds a system-instruction content block from options, or null when none is set.</summary>
        private static Content? BuildSystemInstruction(GeminiRequestOptions? options)
        {
            if (string.IsNullOrWhiteSpace(options?.SystemInstruction))
            {
                return null;
            }

            return new Content
            {
                Parts = new List<Part> { new() { Text = options!.SystemInstruction } }
            };
        }

        /// <summary>
        /// Assembles the tools list: an explicit <see cref="GeminiRequestOptions.Tools"/> wins; otherwise
        /// tools are built from declared functions and the Enable* flags. Returns null when there are none.
        /// </summary>
        private static List<Tool>? BuildTools(GeminiRequestOptions? options)
        {
            if (options is null)
            {
                return null;
            }

            if (options.Tools is { Count: > 0 })
            {
                return options.Tools;
            }

            var tools = new List<Tool>();

            if (options.Functions is { Count: > 0 })
            {
                tools.Add(new Tool { FunctionDeclarations = options.Functions });
            }
            if (options.EnableGoogleSearch)
            {
                tools.Add(new Tool { GoogleSearch = new GoogleSearchTool() });
            }
            if (options.EnableUrlContext)
            {
                tools.Add(new Tool { UrlContext = new UrlContextTool() });
            }
            if (options.EnableCodeExecution)
            {
                tools.Add(new Tool { CodeExecution = new CodeExecutionTool() });
            }

            return tools.Count > 0 ? tools : null;
        }

        /// <summary>Builds a thinking config from options, or null when neither thinking option is set.</summary>
        private static ThinkingConfig? BuildThinkingConfig(GeminiRequestOptions options)
        {
            if (options.ThinkingBudget is null && options.ThinkingLevel is null && options.IncludeThoughts is null)
            {
                return null;
            }

            // The API rejects requests that set both; fail fast with a clear message.
            if (options.ThinkingBudget is not null && options.ThinkingLevel is not null)
            {
                throw new ArgumentException(
                    "Set only one of ThinkingBudget (Gemini 2.5) or ThinkingLevel (Gemini 3+), not both.",
                    nameof(options));
            }

            return new ThinkingConfig
            {
                ThinkingBudget = options.ThinkingBudget,
                ThinkingLevel = options.ThinkingLevel,
                IncludeThoughts = options.IncludeThoughts
            };
        }

        /// <summary>
        /// Gets default safety settings
        /// </summary>
        private static List<SafetySetting> GetDefaultSafetySettings()
        {
            return ConfigurationUtilities.CreateSafetySettings(ConfigurationUtilities.GetDefaultSafetyThresholds());
        }

        /// <summary>
        /// Creates a simple text request for token counting (without generation config or safety settings)
        /// </summary>
        public static CountTokensRequest CreateTokenCountingTextRequest(string text)
        {
            var content = new Content
            {
                Role = "user",
                Parts = new List<Part> { new() { Text = text } }
            };

            return new CountTokensRequest
            {
                Contents = new List<Content> { content }
            };
        }

        /// <summary>
        /// Creates a vision request for token counting (text + image, without generation config or safety settings)
        /// </summary>
        public static CountTokensRequest CreateTokenCountingVisionRequest(string text, FileObject image)
        {
            var parts = new List<Part>
            {
                new() { Text = text },
                new() 
                { 
                    InlineData = new InlineData
                    {
                        Data = Convert.ToBase64String(image.FileContent),
                        MimeType = FileUtilities.GetMimeType(image.FileName)
                    }
                }
            };

            var content = new Content
            {
                Role = "user",
                Parts = parts
            };

            return new CountTokensRequest
            {
                Contents = new List<Content> { content }
            };
        }

        /// <summary>
        /// Creates a chat request for token counting (without generation config or safety settings)
        /// </summary>
        public static CountTokensRequest CreateTokenCountingChatRequest(MessageObject[] messages)
        {
            var contents = messages.Select(msg => new Content
            {
                Role = msg.Role,
                Parts = new List<Part> { new() { Text = msg.Text } }
            }).ToList();

            return new CountTokensRequest
            {
                Contents = contents
            };
        }

        /// <summary>
        /// Creates a token-counting request directly from a raw list of <see cref="Content"/> turns
        /// (without generation config or safety settings). The counterpart to <see cref="CreateRequest"/>
        /// for the <c>countTokens</c> endpoint, used to estimate cost for the raw-<c>Content</c>
        /// overload of <c>IGeminiService.ChatAsync</c>, the same way the other
        /// <c>CreateTokenCounting*Request</c> methods support their matching generation overloads.
        /// </summary>
        public static CountTokensRequest CreateTokenCountingRequest(IEnumerable<Content> contents)
        {
            return new CountTokensRequest
            {
                Contents = contents.ToList()
            };
        }

        /// <summary>
        /// Creates an embedding request for single text input (without generation config or safety settings)
        /// </summary>
        public static SingleEmbedContentRequest CreateEmbeddingRequest(string text, EmbeddingOptions? options = null)
        {
            var content = new Content
            {
                Role = "user",
                Parts = new List<Part> { new() { Text = text } }
            };

            return new SingleEmbedContentRequest
            {
                Content = content,
                TaskType = options?.TaskType,
                Title = options?.Title,
                OutputDimensionality = options?.OutputDimensionality
            };
        }

        /// <summary>
        /// Creates embedding content for batch requests
        /// </summary>
        public static Content CreateEmbeddingContent(string text)
        {
            return new Content
            {
                Role = "user",
                Parts = new List<Part> { new() { Text = text } }
            };
        }
    }
}