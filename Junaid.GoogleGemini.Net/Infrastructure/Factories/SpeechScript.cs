using System.Text.RegularExpressions;
using Junaid.GoogleGemini.Net.Models.GoogleApi;
using Junaid.GoogleGemini.Net.Models.Requests;

namespace Junaid.GoogleGemini.Net.Infrastructure.Factories;

/// <summary>
/// Builds the text parts of a TTS request. The Gemini 3.8 TTS models and the older TTS models need two
/// different shapes, found live on 2026-10-10 (see <c>PLAN-tts-3-8.md</c> §2):
/// <list type="bullet">
/// <item><description>
/// 3.8 models read the text as a verbatim transcript. A multi-speaker request must send one part per
/// turn, each with <c>speechMetadata.speaker</c>. The older "Name: line" labels inside one text part
/// return HTTP 400.
/// </description></item>
/// <item><description>
/// The three older models (2.5 Flash, 2.5 Pro and 3.1 Flash TTS) reject <c>speechMetadata</c> with
/// HTTP 400 and still need the labels inside one text part.
/// </description></item>
/// </list>
/// </summary>
internal static class SpeechScript
{
    // The three TTS models that Google shuts down on 2026-11-17. String literals, not the
    // GeminiConstants members, because those members are [Obsolete] and this project treats
    // warnings as errors.
    private static readonly string[] LegacyTtsModels =
    {
        "gemini-2.5-flash-preview-tts",
        "gemini-2.5-pro-preview-tts",
        "gemini-3.1-flash-tts-preview"
    };

    /// <summary>
    /// True when the request should use <c>speechMetadata</c>. An explicit
    /// <see cref="GeminiRequestOptions.UseSpeechMetadata"/> wins. Otherwise only the three older TTS
    /// models say no. A model name we do not know says yes, because every TTS model released after
    /// the 3.8 generation is expected to follow the newer shape.
    /// </summary>
    internal static bool UsesSpeechMetadata(GeminiRequestOptions options)
    {
        if (options.UseSpeechMetadata is { } forced)
        {
            return forced;
        }

        var model = options.Model;
        if (string.IsNullOrEmpty(model))
        {
            return true;
        }

        if (model!.StartsWith("models/", StringComparison.OrdinalIgnoreCase))
        {
            model = model.Substring("models/".Length);
        }

        return !LegacyTtsModels.Contains(model, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Builds the text parts for a request from the prompt and the TTS options.</summary>
    /// <exception cref="InvalidOperationException">
    /// <see cref="GeminiRequestOptions.SpeechStyle"/> is set but the request uses the older shape, which
    /// has nowhere to send a style.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <see cref="GeminiRequestOptions.SpeakerVoices"/> is set, the request uses <c>speechMetadata</c>,
    /// and the prompt has no <c>Name:</c> marker for any listed speaker.
    /// </exception>
    internal static List<Part> BuildTextParts(string text, GeminiRequestOptions? options)
    {
        var hasSpeakers = options?.SpeakerVoices is { Count: > 0 };
        var hasStyle = !string.IsNullOrWhiteSpace(options?.SpeechStyle);

        if (options is null || (!hasSpeakers && !hasStyle))
        {
            return new List<Part> { new() { Text = text } };
        }

        if (!UsesSpeechMetadata(options))
        {
            if (hasStyle)
            {
                throw new InvalidOperationException(
                    "GeminiRequestOptions.SpeechStyle needs a Gemini 3.8 or newer TTS model. " +
                    $"The model '{options.Model}' rejects speech metadata. " +
                    "Write the direction in the prompt text instead, for example \"Say cheerfully: ...\".");
            }

            // Older model with a multi-speaker script: send the prompt as written.
            return new List<Part> { new() { Text = text } };
        }

        var style = hasStyle ? options.SpeechStyle : null;

        if (!hasSpeakers)
        {
            return new List<Part>
            {
                new() { Text = text, SpeechMetadata = new SpeechMetadata { Style = style } }
            };
        }

        var turns = Split(text, options.SpeakerVoices!.Select(sv => sv.Speaker));
        if (turns.Count == 0)
        {
            throw new ArgumentException(
                "SpeakerVoices is set, but the prompt has no line that starts with one of these speaker names " +
                $"followed by a colon: {string.Join(", ", options.SpeakerVoices!.Select(sv => sv.Speaker))}. " +
                "Write the script as \"Joe: Hi Jane! Jane: Hello Joe!\".",
                nameof(text));
        }

        return turns
            .Select(t => new Part
            {
                Text = t.Text,
                SpeechMetadata = new SpeechMetadata { Speaker = t.Speaker, Style = style }
            })
            .ToList();
    }

    /// <summary>
    /// Splits a script at each <c>Name:</c> marker. A marker is a listed speaker name, matched exactly
    /// including case, that does not follow a letter, digit or underscore, followed by optional spaces
    /// and a colon. Text before the first marker is dropped. A turn with no text is skipped.
    /// </summary>
    internal static List<(string Speaker, string Text)> Split(string script, IEnumerable<string> speakers)
    {
        var names = speakers
            .Where(s => !string.IsNullOrEmpty(s))
            .Distinct(StringComparer.Ordinal)
            .OrderByDescending(s => s.Length)
            .Select(Regex.Escape)
            .ToList();

        var turns = new List<(string Speaker, string Text)>();
        if (names.Count == 0)
        {
            return turns;
        }

        var pattern = @"(?<![\p{L}\p{N}_])(" + string.Join("|", names) + @")[ \t]*:";
        var matches = Regex.Matches(script, pattern, RegexOptions.CultureInvariant);

        for (var i = 0; i < matches.Count; i++)
        {
            var start = matches[i].Index + matches[i].Length;
            var end = i + 1 < matches.Count ? matches[i + 1].Index : script.Length;
            var turnText = script.Substring(start, end - start).Trim();
            if (turnText.Length > 0)
            {
                turns.Add((matches[i].Groups[1].Value, turnText));
            }
        }

        return turns;
    }
}
