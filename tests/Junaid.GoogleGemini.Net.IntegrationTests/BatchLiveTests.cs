using Junaid.GoogleGemini.Net.Infrastructure.Utilities;
using Junaid.GoogleGemini.Net.Models.GoogleApi;
using Junaid.GoogleGemini.Net.Services;
using Junaid.GoogleGemini.Net.Services.Interfaces;
using Xunit;

namespace Junaid.GoogleGemini.Net.IntegrationTests;

/// <summary>
/// Live tests against the real Batch API. Deliberately fast/non-blocking — see
/// <c>PLAN-batch-api.md</c> §7: Google's own target turnaround is "24 hours, often faster," with no
/// hard SLA and up to 48 hours before a job auto-expires, so no test here waits for a job to actually
/// complete. What these tests exist to resolve is the stuff docs alone couldn't settle: the exact
/// <c>state</c> string prefix (JOB_STATE_ vs BATCH_STATE_, see <see cref="BatchJob.State"/>'s remarks)
/// and whether the create request body's shape (top-level "batch" wrapper, no model field) is actually
/// accepted. Uses <see cref="RequiresPaidGeminiKeyAttribute"/>, not the plain key-only one — the Batch
/// API is documented as unavailable on the free tier.
/// </summary>
[Collection("Live")]
public class BatchLiveTests(GeminiFixture fixture)
{
    private IBatchService Batch => fixture.Get<IBatchService>();

    [RequiresPaidGeminiKey]
    public async Task CreateAsync_ThenGetAsync_ResolvesActualStateStringAndAcceptsRequestShape()
    {
        var requests = new List<InlinedBatchRequest>
        {
            new()
            {
                Request = new GenerateContentRequest
                {
                    Contents = { new Content { Parts = { new Part { Text = "Reply with exactly the word: pong" } } } }
                }
            }
        };

        var created = await Batch.CreateAsync(
            GeminiConstants.Models.Gemini35FlashLite, requests, "junaid-googlegemini-net-live-test");

        // If we got here at all, the create call succeeded: the request body (top-level "batch"
        // wrapper, no model field — see CreateBatchRequest) was accepted by the real API.
        Assert.False(string.IsNullOrWhiteSpace(created.Name));

        var fetched = await Batch.GetAsync(created.Name!);

        // The actual fact this test exists to pin down: whatever prefix Google's API really uses,
        // IsTerminalState/BatchService must recognize it (or, for a freshly created job, correctly
        // recognize it does NOT look terminal yet).
        Assert.False(string.IsNullOrWhiteSpace(fetched.State));
        Assert.False(BatchService.IsTerminalState(fetched.State)); // freshly created; shouldn't be terminal immediately

        // Cleanup: don't leave a live job (and its eventual real spend) behind just from running tests.
        await Batch.DeleteAsync(created.Name!);
    }

    [RequiresPaidGeminiKey]
    public async Task CreateAsync_ThenCancelAsync_TransitionsTowardCancelled()
    {
        var requests = new List<InlinedBatchRequest>
        {
            new()
            {
                Request = new GenerateContentRequest
                {
                    Contents = { new Content { Parts = { new Part { Text = "Reply with exactly the word: pong" } } } }
                }
            }
        };

        var created = await Batch.CreateAsync(
            GeminiConstants.Models.Gemini35FlashLite, requests, "junaid-googlegemini-net-live-test-cancel");

        await Batch.CancelAsync(created.Name!);

        // Cancellation is asynchronous on Google's side too (see IBatchService.CancelAsync's remarks),
        // so this doesn't assert an exact terminal "CANCELLED" state immediately — only that the call
        // itself succeeded (no exception) and the job is still gettable afterward.
        var fetched = await Batch.GetAsync(created.Name!);
        Assert.False(string.IsNullOrWhiteSpace(fetched.State));

        await Batch.DeleteAsync(created.Name!);
    }

    /// <summary>
    /// The one live test that waits for a real completion, rather than a fast create+get/cancel check.
    /// Justified here (contrary to the general "don't block on real completion" guidance elsewhere in
    /// this suite - see PLAN-batch-api.md §7): a trivial 1-request job was empirically observed to
    /// complete in under two minutes during live verification on 2026-08-15, so a bounded ~5 minute
    /// wait is a reasonable regression test, not an open-ended one. This is what actually proved the
    /// results-parsing path (inline results, real usage metadata, real generated text) works end to
    /// end against the real API, not just against fabricated fixtures.
    /// </summary>
    [RequiresPaidGeminiKey]
    public async Task CreateAsync_WaitUntilComplete_ReturnsRealGeneratedText()
    {
        var requests = new List<InlinedBatchRequest>
        {
            new()
            {
                Request = new GenerateContentRequest
                {
                    Contents = { new Content { Parts = { new Part { Text = "Reply with exactly the word: pong" } } } }
                }
            }
        };

        var created = await Batch.CreateAsync(
            GeminiConstants.Models.Gemini35FlashLite, requests, "junaid-googlegemini-net-live-test-full-roundtrip");

        var finished = await Batch.WaitUntilCompleteAsync(
            created.Name!, pollInterval: TimeSpan.FromSeconds(20), timeout: TimeSpan.FromMinutes(5));

        Assert.True(BatchService.IsTerminalState(finished.State));
        Assert.True(finished.Done);
        Assert.Null(finished.Error);
        Assert.NotNull(finished.BatchStats);
        Assert.Equal(1, finished.BatchStats!.SuccessfulRequestCount); // confirms batchStats' string-typed numbers parse correctly

        var results = await Batch.GetResultsAsync(finished);

        Assert.Single(results);
        Assert.NotNull(results[0].Response);
        Assert.Contains("pong", results[0].Response!.Text(), StringComparison.OrdinalIgnoreCase);
        Assert.True(results[0].Response!.Usage?.TotalTokenCount > 0);

        await Batch.DeleteAsync(created.Name!);
    }
}
