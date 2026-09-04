using Junaid.GoogleGemini.Net.Models.GoogleApi;
using Junaid.GoogleGemini.Net.Models.Requests;

namespace Junaid.GoogleGemini.Net.Services.Interfaces
{
    /// <summary>
    /// Unified service interface for all Gemini content generation operations
    /// </summary>
    public interface IGeminiService
    {
        /// <summary>
        /// Generates content based on text input
        /// </summary>
        /// <param name="prompt">The text prompt</param>
        /// <param name="options">Optional generation options</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task<GenerateContentResponse> GenerateAsync(
            string prompt,
            GeminiRequestOptions? options = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Generates content and deserializes the model's JSON response into <typeparamref name="T"/>.
        /// A response schema is derived from <typeparamref name="T"/> automatically (unless you set one
        /// in <paramref name="options"/>), and the request is constrained to JSON output.
        /// </summary>
        /// <typeparam name="T">The shape to return. Use a class/record with simple properties.</typeparam>
        /// <param name="prompt">The text prompt</param>
        /// <param name="options">Optional generation options (schema/MIME type are filled in if unset)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task<T> GenerateAsync<T>(
            string prompt,
            GeminiRequestOptions? options = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Generates content based on text and image input
        /// </summary>
        /// <param name="prompt">The text prompt</param>
        /// <param name="image">The image data</param>
        /// <param name="options">Optional generation options</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task<GenerateContentResponse> GenerateWithImageAsync(
            string prompt,
            FileObject image,
            GeminiRequestOptions? options = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Generates one or more images from a text prompt (Gemini "Nano Banana" image models). Get the
        /// result via <see cref="GenerateContentResponse.Images"/>/<see cref="GenerateContentResponse.TryGetImages"/>/
        /// <see cref="GenerateContentResponse.GetImagesOrThrow"/> on the response.
        /// </summary>
        /// <param name="prompt">The image prompt</param>
        /// <param name="options">
        /// Optional generation options. If <see cref="GeminiRequestOptions.Model"/> is unset, defaults
        /// to <c>GeminiConstants.Models.RecommendedImage</c>; if <see cref="GeminiRequestOptions.ResponseModalities"/>
        /// is unset, defaults to <c>[TEXT, IMAGE]</c> (works on both older and current image models).
        /// Set <see cref="GeminiRequestOptions.ImageAspectRatio"/>/<see cref="GeminiRequestOptions.ImageSize"/>
        /// for Gemini 3+ image models.
        /// </param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task<GenerateContentResponse> GenerateImageAsync(
            string prompt,
            GeminiRequestOptions? options = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Generates spoken audio from text (Gemini TTS models). Get the result via
        /// <see cref="GenerateContentResponse.Audio"/>/<see cref="GenerateContentResponse.TryGetAudio"/>/
        /// <see cref="GenerateContentResponse.GetAudioOrThrow"/> on the response, then
        /// <see cref="GeneratedAudio.ToWav"/> to get a playable file.
        /// </summary>
        /// <param name="prompt">
        /// The text to speak. For multi-speaker audio (see <see cref="GeminiRequestOptions.SpeakerVoices"/>),
        /// this should name each speaker as they appear in the text, e.g. "Joe: Hi! Jane: Hello!".
        /// </param>
        /// <param name="options">
        /// Optional generation options. If <see cref="GeminiRequestOptions.Model"/> is unset, defaults
        /// to <c>GeminiConstants.Models.RecommendedTts</c>; if <see cref="GeminiRequestOptions.ResponseModalities"/>
        /// is unset, defaults to <c>[AUDIO]</c>. Set <see cref="GeminiRequestOptions.VoiceName"/> for a
        /// single voice, or <see cref="GeminiRequestOptions.SpeakerVoices"/> for a multi-speaker script.
        /// </param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task<GenerateContentResponse> GenerateAudioAsync(
            string prompt,
            GeminiRequestOptions? options = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Generates content based on chat history
        /// </summary>
        /// <param name="messages">Array of chat messages</param>
        /// <param name="options">Optional generation options</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task<GenerateContentResponse> ChatAsync(
            MessageObject[] messages,
            GeminiRequestOptions? options = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Generates content from a raw list of <see cref="Content"/> turns. Use this for full
        /// multi-turn control, including echoing back the model's previous response parts (function
        /// calls/responses and Gemini 3 <c>thoughtSignature</c> values), which the simple text-based
        /// <see cref="ChatAsync(MessageObject[], GeminiRequestOptions?, CancellationToken)"/> can't carry.
        /// </summary>
        /// <remarks>
        /// Covered by <see cref="Infrastructure.Options.BudgetOptions.MaxCostPerRequestUsd"/> like the
        /// other generation overloads, via <see cref="CountTokensChatAsync(IList{Content}, GeminiRequestOptions?, CancellationToken)"/>
        /// for the pre-flight estimate. The same general estimate caveats apply (standard-rate-only
        /// input pricing, output bounded only when <see cref="GeminiRequestOptions.MaxTokens"/> is set.
        /// See the XML docs on <see cref="Infrastructure.Options.BudgetOptions.MaxCostPerRequestUsd"/>).
        /// </remarks>
        Task<GenerateContentResponse> ChatAsync(
            IList<Content> contents,
            GeminiRequestOptions? options = null,
            CancellationToken cancellationToken = default);

        /// <summary>Streaming counterpart of <see cref="ChatAsync(IList{Content}, GeminiRequestOptions?, CancellationToken)"/>.</summary>
        /// <remarks>Same <c>MaxCostPerRequestUsd</c> coverage as the non-streaming overload above.</remarks>
        IAsyncEnumerable<GenerateContentResponse> StreamChatAsync(
            IList<Content> contents,
            GeminiRequestOptions? options = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Streams content generation for a text prompt, yielding each response chunk as it arrives.
        /// Iterate with <c>await foreach</c>; use <c>chunk.Text()</c> for the text of each chunk.
        /// </summary>
        /// <param name="prompt">The text prompt</param>
        /// <param name="options">Optional generation options</param>
        /// <param name="cancellationToken">Cancellation token</param>
        IAsyncEnumerable<GenerateContentResponse> StreamAsync(
            string prompt,
            GeminiRequestOptions? options = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Streams content generation for a text + image prompt, yielding each response chunk.
        /// </summary>
        IAsyncEnumerable<GenerateContentResponse> StreamWithImageAsync(
            string prompt,
            FileObject image,
            GeminiRequestOptions? options = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Streams audio generation for a text prompt (Gemini TTS models), yielding each response
        /// chunk as it arrives. Same defaults as <see cref="GenerateAudioAsync"/>. A short clip may
        /// arrive as a single chunk; only the final chunk is guaranteed to carry <see cref="GenerateContentResponse.Usage"/>.
        /// </summary>
        IAsyncEnumerable<GenerateContentResponse> StreamAudioAsync(
            string prompt,
            GeminiRequestOptions? options = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Streams content generation for a chat history, yielding each response chunk.
        /// </summary>
        IAsyncEnumerable<GenerateContentResponse> StreamChatAsync(
            MessageObject[] messages,
            GeminiRequestOptions? options = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Convenience overload: streams a text prompt and invokes <paramref name="handleResponse"/>
        /// with the text of each chunk. Prefer the <see cref="IAsyncEnumerable{T}"/> overload for
        /// access to finish reason, usage, and safety data.
        /// </summary>
        Task StreamAsync(
            string prompt,
            Action<string> handleResponse,
            GeminiRequestOptions? options = null,
            CancellationToken cancellationToken = default);

        /// <summary>Convenience callback overload of <see cref="StreamWithImageAsync(string, FileObject, GeminiRequestOptions?, CancellationToken)"/>.</summary>
        Task StreamWithImageAsync(
            string prompt,
            FileObject image,
            Action<string> handleResponse,
            GeminiRequestOptions? options = null,
            CancellationToken cancellationToken = default);

        /// <summary>Convenience callback overload of <see cref="StreamChatAsync(MessageObject[], GeminiRequestOptions?, CancellationToken)"/>.</summary>
        Task StreamChatAsync(
            MessageObject[] messages,
            Action<string> handleResponse,
            GeminiRequestOptions? options = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Counts tokens for text input, against the default model
        /// (<see cref="Infrastructure.Options.GeminiOptions.DefaultModel"/>).
        /// </summary>
        /// <param name="prompt">The text prompt</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task<CountTokensResponse> CountTokensAsync(
            string prompt,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Counts tokens for text input against a specific model.
        /// </summary>
        /// <param name="prompt">The text prompt</param>
        /// <param name="options">
        /// Options used to resolve the model to count against (<see cref="GeminiRequestOptions.Model"/>).
        /// Falls back to the default model when <paramref name="options"/> or its <c>Model</c> is null.
        /// Other fields on <paramref name="options"/> are ignored. Only <c>Model</c> is used.
        /// </param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task<CountTokensResponse> CountTokensAsync(
            string prompt,
            GeminiRequestOptions? options,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Counts tokens for text and image input, against the default model
        /// (<see cref="Infrastructure.Options.GeminiOptions.DefaultModel"/>).
        /// </summary>
        /// <param name="prompt">The text prompt</param>
        /// <param name="image">The image data</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task<CountTokensResponse> CountTokensWithImageAsync(
            string prompt,
            FileObject image,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Counts tokens for text and image input against a specific model.
        /// </summary>
        /// <param name="prompt">The text prompt</param>
        /// <param name="image">The image data</param>
        /// <param name="options">
        /// Options used to resolve the model to count against (<see cref="GeminiRequestOptions.Model"/>).
        /// Falls back to the default model when <paramref name="options"/> or its <c>Model</c> is null.
        /// Other fields on <paramref name="options"/> are ignored. Only <c>Model</c> is used.
        /// </param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task<CountTokensResponse> CountTokensWithImageAsync(
            string prompt,
            FileObject image,
            GeminiRequestOptions? options,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Counts tokens for chat messages, against the default model
        /// (<see cref="Infrastructure.Options.GeminiOptions.DefaultModel"/>).
        /// </summary>
        /// <param name="messages">Array of chat messages</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task<CountTokensResponse> CountTokensChatAsync(
            MessageObject[] messages,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Counts tokens for chat messages against a specific model.
        /// </summary>
        /// <param name="messages">Array of chat messages</param>
        /// <param name="options">
        /// Options used to resolve the model to count against (<see cref="GeminiRequestOptions.Model"/>).
        /// Falls back to the default model when <paramref name="options"/> or its <c>Model</c> is null.
        /// Other fields on <paramref name="options"/> are ignored. Only <c>Model</c> is used.
        /// </param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task<CountTokensResponse> CountTokensChatAsync(
            MessageObject[] messages,
            GeminiRequestOptions? options,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Counts tokens for a raw list of <see cref="Content"/> turns against a specific model. This is the
        /// token-counting counterpart to <see cref="ChatAsync(IList{Content}, GeminiRequestOptions?, CancellationToken)"/>.
        /// </summary>
        /// <param name="contents">The content turns to count. Must contain at least one item.</param>
        /// <param name="options">
        /// Options used to resolve the model to count against (<see cref="GeminiRequestOptions.Model"/>).
        /// Falls back to the default model when <paramref name="options"/> or its <c>Model</c> is null.
        /// Other fields on <paramref name="options"/> are ignored. Only <c>Model</c> is used.
        /// </param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task<CountTokensResponse> CountTokensChatAsync(
            IList<Content> contents,
            GeminiRequestOptions? options = null,
            CancellationToken cancellationToken = default);
    }
}