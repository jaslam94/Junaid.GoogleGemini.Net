using Junaid.GoogleGemini.Net.Models.Requests;
using Junaid.GoogleGemini.Net.Services.Interfaces;
using Xunit;

namespace Junaid.GoogleGemini.Net.IntegrationTests;

/// <summary>
/// Live tests for the simple <c>MessageObject[]</c>-based <c>ChatAsync</c> overload -- the everyday
/// multi-turn surface most consumers use. The Content-based overload is already live-tested for
/// Gemini 3 function calling + thoughtSignature round-tripping in
/// <see cref="LiveTests.FunctionCalling_Gemini3_RoundTripWithThoughtSignature"/>; this covers the
/// plain-text conversation-history path, which had no live coverage at all.
/// </summary>
[Collection("Live")]
public class MultiTurnChatLiveTests(GeminiFixture fixture)
{
    private IGeminiService Gemini => fixture.Get<IGeminiService>();

    [RequiresGeminiKey]
    public async Task ChatAsync_RemembersInformationFromEarlierTurns()
    {
        var messages = new[]
        {
            new MessageObject("user", "My favorite number is 47. Just acknowledge that in one short sentence."),
            new MessageObject("model", "Got it, your favorite number is 47."),
            new MessageObject("user", "What is my favorite number? Reply with just the digits."),
        };

        var response = await Gemini.ChatAsync(messages);

        Assert.Equal("STOP", response.FinishReason);
        Assert.Contains("47", response.Text());
    }

    [RequiresGeminiKey]
    public async Task ChatAsync_ThreeTurns_CarriesContextAcrossSeparateCalls()
    {
        // Simulates how a real app accumulates history: each turn's response is appended before the
        // next call, rather than the whole conversation being hand-written up front.
        var history = new List<MessageObject> { new("user", "Remember the codeword ZEBRA-19. Just acknowledge it briefly.") };

        var first = await Gemini.ChatAsync(history.ToArray());
        Assert.False(string.IsNullOrWhiteSpace(first.Text()));
        history.Add(new MessageObject("model", first.Text()));

        history.Add(new MessageObject("user", "Now, separately, what is 8 plus 5? Reply with just the number."));
        var second = await Gemini.ChatAsync(history.ToArray());
        Assert.Contains("13", second.Text());
        history.Add(new MessageObject("model", second.Text()));

        history.Add(new MessageObject("user", "What was the codeword I told you earlier? Reply with just the codeword."));
        var third = await Gemini.ChatAsync(history.ToArray());

        Assert.Contains("ZEBRA-19", third.Text(), StringComparison.OrdinalIgnoreCase);
    }
}
