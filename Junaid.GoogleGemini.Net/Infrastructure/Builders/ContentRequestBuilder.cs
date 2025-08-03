using Junaid.GoogleGemini.Net.Models.GoogleApi;

namespace Junaid.GoogleGemini.Net.Infrastructure.Builders;

/// <summary>
/// Fluent builder for creating GenerateContentRequest objects
/// </summary>
public class ContentRequestBuilder
{
    private readonly List<Content> _contents;
    private readonly GenerationConfig _generationConfig;
    private readonly List<SafetySetting> _safetySettings;
    private readonly List<Part> _currentParts;
    private bool _isStreaming;
    private string? _currentRole;

    public ContentRequestBuilder()
    {
        _contents = new List<Content>();
        _currentParts = new List<Part>();
        _generationConfig = new GenerationConfig();
        _safetySettings = new List<SafetySetting>();
    }

    /// <summary>
    /// Sets the role for the next content block (e.g., "user" or "assistant")
    /// </summary>
    public ContentRequestBuilder WithRole(string role)
    {
        _currentRole = role;
        return this;
    }

    /// <summary>
    /// Adds a text message to the request
    /// </summary>
    public ContentRequestBuilder AddText(string text)
    {
        _currentParts.Add(new Part { Text = text });
        return this;
    }

    /// <summary>
    /// Adds an image file to the request
    /// </summary>
    public ContentRequestBuilder AddImage(string base64Image, string mimeType)
    {
        _currentParts.Add(new Part
        {
            InlineData = new InlineData
            {
                Data = base64Image,
                MimeType = mimeType
            }
        });
        return this;
    }

    /// <summary>
    /// Finalizes the current message and adds it to the content list
    /// </summary>
    public ContentRequestBuilder AddMessage()
    {
        if (_currentParts.Count > 0)
        {
            _contents.Add(new Content
            {
                Role = _currentRole,
                Parts = new List<Part>(_currentParts)
            });
            _currentParts.Clear();
            _currentRole = null;
        }
        return this;
    }

    /// <summary>
    /// Sets the generation configuration
    /// </summary>
    public ContentRequestBuilder WithConfiguration(Action<GenerationConfig> configAction)
    {
        configAction(_generationConfig);
        return this;
    }

    /// <summary>
    /// Sets the temperature for response generation (0.0 to 1.0)
    /// </summary>
    public ContentRequestBuilder WithTemperature(float temperature)
    {
        _generationConfig.Temperature = temperature;
        return this;
    }

    /// <summary>
    /// Sets the candidate count for response generation
    /// </summary>
    public ContentRequestBuilder WithCandidateCount(int count)
    {
        _generationConfig.CandidateCount = count;
        return this;
    }

    /// <summary>
    /// Sets the maximum output tokens
    /// </summary>
    public ContentRequestBuilder WithMaxOutputTokens(int tokens)
    {
        _generationConfig.MaxOutputTokens = tokens;
        return this;
    }

    /// <summary>
    /// Sets the top-k parameter for response generation
    /// </summary>
    public ContentRequestBuilder WithTopK(int topK)
    {
        _generationConfig.TopK = topK;
        return this;
    }

    /// <summary>
    /// Sets the top-p parameter for response generation
    /// </summary>
    public ContentRequestBuilder WithTopP(float topP)
    {
        _generationConfig.TopP = topP;
        return this;
    }

    /// <summary>
    /// Sets the stop sequences for response generation
    /// </summary>
    public ContentRequestBuilder WithStopSequences(params string[] sequences)
    {
        _generationConfig.StopSequences = sequences.ToList();
        return this;
    }

    /// <summary>
    /// Adds a safety setting to the request
    /// </summary>
    public ContentRequestBuilder WithSafetySetting(string category, string threshold)
    {
        _safetySettings.Add(new SafetySetting
        {
            Category = category,
            Threshold = threshold
        });
        return this;
    }

    /// <summary>
    /// Adds multiple safety settings to the request
    /// </summary>
    public ContentRequestBuilder WithSafetySettings(IEnumerable<SafetySetting> safetySettings)
    {
        _safetySettings.AddRange(safetySettings);
        return this;
    }

    /// <summary>
    /// Enables streaming mode for the request
    /// </summary>
    public ContentRequestBuilder EnableStreaming(bool enable = true)
    {
        _isStreaming = enable;
        return this;
    }

    /// <summary>
    /// Builds the GenerateContentRequest object
    /// </summary>
    public GenerateContentRequest Build()
    {
        // Add any remaining message
        AddMessage();

        if (_contents.Count == 0)
        {
            throw new InvalidOperationException("Request must contain at least one message");
        }

        return new GenerateContentRequest
        {
            Contents = _contents,
            GenerationConfig = _generationConfig,
            SafetySettings = _safetySettings.Count > 0 ? _safetySettings : null
        };
    }
}