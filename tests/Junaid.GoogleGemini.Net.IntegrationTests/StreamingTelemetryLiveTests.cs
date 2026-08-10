using System.Diagnostics;
using Junaid.GoogleGemini.Net.Infrastructure.Telemetry;
using Junaid.GoogleGemini.Net.Infrastructure.Utilities;
using Junaid.GoogleGemini.Net.Models.Requests;
using Junaid.GoogleGemini.Net.Services.Interfaces;
using Xunit;

namespace Junaid.GoogleGemini.Net.IntegrationTests;

/// <summary>
/// Live test confirming <c>StreamAsync</c> now emits a real OpenTelemetry span (implemented this
/// session -- previously StreamAsync had zero tracing, confirmed by an explicit code comment; only
/// PostAsync was instrumented). Uses a real <see cref="ActivityListener"/>, the same mechanism a
/// consumer's OTel SDK setup would use, rather than inspecting internals.
/// </summary>
[Collection("Live")]
public class StreamingTelemetryLiveTests(GeminiFixture fixture)
{
    private IGeminiService Gemini => fixture.Get<IGeminiService>();

    [RequiresGeminiKey]
    public async Task StreamAsync_EmitsSpanWithUsageTagsAndOkStatus()
    {
        var recorded = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == GeminiTelemetry.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => recorded.Add(activity),
        };
        ActivitySource.AddActivityListener(listener);

        var options = new GeminiRequestOptions { MaxTokens = 200, ThinkingLevel = GeminiConstants.ThinkingLevels.Minimal };
        var chunkCount = 0;
        await foreach (var _ in Gemini.StreamAsync("Count from 1 to 5, one number per line.", options))
        {
            chunkCount++;
        }
        Assert.True(chunkCount > 0);

        var span = Assert.Single(recorded, a => a.OperationName.Contains("streamGenerateContent"));
        // GeminiClient never explicitly sets ActivityStatusCode.Ok on success (same convention as
        // PostAsync) -- Error is only set on failure paths, so Unset is the expected "it worked" state.
        Assert.NotEqual(ActivityStatusCode.Error, span.Status);
        Assert.Equal("gemini", span.Tags.FirstOrDefault(t => t.Key == "gen_ai.system").Value);

        // NOTE: gen_ai.usage.input_tokens/output_tokens are set via SetTag(string, object) with an int
        // value, not a string -- Activity.Tags (the legacy string-only projection checked above) only
        // surfaces string-valued tags and silently omits these, even though SetTag genuinely set them.
        // A real OTel exporter reads TagObjects (the full, object-typed view), which is what this
        // checks. Confirmed empirically while writing this test: even a hardcoded literal int tag was
        // invisible via .Tags but present via .TagObjects -- not a library bug, a wrong API to assert against.
        var tagObjects = span.TagObjects.ToDictionary(t => t.Key, t => t.Value);
        Assert.True((int)tagObjects["gen_ai.usage.input_tokens"]! > 0);
        Assert.True((int)tagObjects["gen_ai.usage.output_tokens"]! > 0);
        Assert.True(span.Duration > TimeSpan.Zero);
    }
}
