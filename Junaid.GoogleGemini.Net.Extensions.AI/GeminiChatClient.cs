using System.Runtime.CompilerServices;
using Junaid.GoogleGemini.Net.Models.GoogleApi;
using Junaid.GoogleGemini.Net.Models.Requests;
using Junaid.GoogleGemini.Net.Services.Interfaces;
using Microsoft.Extensions.AI;

namespace Junaid.GoogleGemini.Net.Extensions.AI;

/// <summary>
/// Adapts <see cref="IGeminiService"/> to <see cref="IChatClient"/> so Gemini can be used anywhere the
/// Microsoft.Extensions.AI abstractions are consumed (Semantic Kernel, agent frameworks, middleware).
/// </summary>
public sealed class GeminiChatClient : IChatClient
{
    private readonly IGeminiService _service;
    private readonly string? _defaultModel;

    /// <summary>Creates a chat client over the given Gemini service.</summary>
    /// <param name="service">The underlying Gemini service.</param>
    /// <param name="defaultModel">Model id to use when a request doesn't specify one.</param>
    public GeminiChatClient(IGeminiService service, string? defaultModel = null)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _defaultModel = defaultModel;
    }

    /// <inheritdoc/>
    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var (history, systemInstruction) = MapMessages(messages);
        var requestOptions = MapOptions(options, systemInstruction);

        var response = await _service.ChatAsync(history, requestOptions, cancellationToken);

        var assistant = new ChatMessage(ChatRole.Assistant, response.Text());
        return new ChatResponse(assistant)
        {
            ModelId = options?.ModelId ?? _defaultModel,
            FinishReason = MapFinishReason(response.FinishReason),
            Usage = MapUsage(response.Usage),
            RawRepresentation = response,
        };
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var (history, systemInstruction) = MapMessages(messages);
        var requestOptions = MapOptions(options, systemInstruction);

        await foreach (var chunk in _service.StreamChatAsync(history, requestOptions, cancellationToken))
        {
            var text = chunk.Text();
            if (string.IsNullOrEmpty(text))
            {
                continue;
            }

            yield return new ChatResponseUpdate(ChatRole.Assistant, text)
            {
                ModelId = options?.ModelId ?? _defaultModel,
                RawRepresentation = chunk,
            };
        }
    }

    /// <inheritdoc/>
    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        return serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        // The underlying service/HTTP lifetime is owned by DI; nothing to dispose here.
    }

    private static (MessageObject[] History, string? SystemInstruction) MapMessages(IEnumerable<ChatMessage> messages)
    {
        var history = new List<MessageObject>();
        string? systemInstruction = null;

        foreach (var message in messages)
        {
            var text = message.Text;
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            if (message.Role == ChatRole.System)
            {
                systemInstruction = systemInstruction is null ? text : systemInstruction + "\n" + text;
            }
            else
            {
                // Gemini roles are "user" and "model"; map assistant -> model, everything else -> user.
                var role = message.Role == ChatRole.Assistant ? "model" : "user";
                history.Add(new MessageObject(role, text));
            }
        }

        return (history.ToArray(), systemInstruction);
    }

    private GeminiRequestOptions MapOptions(ChatOptions? options, string? systemInstruction)
    {
        var result = new GeminiRequestOptions
        {
            Model = options?.ModelId ?? _defaultModel,
            SystemInstruction = systemInstruction,
        };

        if (options is null)
        {
            return result;
        }

        result.Temperature = options.Temperature;
        result.TopP = options.TopP;
        result.TopK = options.TopK;
        result.MaxTokens = options.MaxOutputTokens;
        if (options.StopSequences is { Count: > 0 })
        {
            result.StopSequences = new List<string>(options.StopSequences);
        }
        if (options.Seed is { } seed)
        {
            result.Seed = unchecked((int)seed);
        }

        return result;
    }

    private static ChatFinishReason? MapFinishReason(string? finishReason) => finishReason switch
    {
        "STOP" => ChatFinishReason.Stop,
        "MAX_TOKENS" => ChatFinishReason.Length,
        "SAFETY" or "RECITATION" or "BLOCKLIST" or "PROHIBITED_CONTENT" => ChatFinishReason.ContentFilter,
        _ => null,
    };

    private static UsageDetails? MapUsage(UsageMetadata? usage)
    {
        if (usage is null)
        {
            return null;
        }

        return new UsageDetails
        {
            InputTokenCount = usage.PromptTokenCount,
            OutputTokenCount = usage.CandidatesTokenCount,
            TotalTokenCount = usage.TotalTokenCount,
        };
    }
}
