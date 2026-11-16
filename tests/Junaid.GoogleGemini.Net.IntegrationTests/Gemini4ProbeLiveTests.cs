using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Junaid.GoogleGemini.Net.Models.Requests;
using Junaid.GoogleGemini.Net.Services.Interfaces;
using Xunit;
using Xunit.Abstractions;

namespace Junaid.GoogleGemini.Net.IntegrationTests;

/// <summary>
/// Probes of the three Gemini 4 models, for <c>PLAN-gemini-4.md</c>. They exist to find out what
/// Google really does, so they print what comes back and assert almost nothing. Run them on their own:
/// <c>gh workflow run integration.yml --ref BRANCH -f paid=true -f filter=FullyQualifiedName~Gemini4Probe</c>.
///
/// Cost: every probe except the continuation probe costs a fraction of a cent. The continuation probe
/// asks for up to 70,000 output tokens on <c>gemini-4-carbon</c>. That is about $0.70 before
/// 2026-12-01 and about twice that after, and it stops after four hops.
/// The probes call the REST API directly where the library has no field yet (the continuation token).
/// </summary>
/// <summary>
/// Skips unless the key, the paid-tier flag and <c>GeminiProbe=1</c> are all set. The workflow sets
/// <c>GeminiProbe</c> only when you give it a <c>filter</c>, so an ordinary full run never pays for a probe.
/// </summary>
public sealed class RequiresProbeOptInAttribute : FactAttribute
{
    public RequiresProbeOptInAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GeminiApiKey")))
        {
            Skip = "Set the GeminiApiKey environment variable to run live integration tests.";
        }
        else if (Environment.GetEnvironmentVariable("GeminiPaidTier") != "1")
        {
            Skip = "Carbon and Argon are paid-only. Set GeminiPaidTier=1 to run.";
        }
        else if (Environment.GetEnvironmentVariable("GeminiProbe") != "1")
        {
            Skip = "A probe costs money and prints results for a plan. Set GeminiProbe=1 (the workflow does this when you pass a filter).";
        }
    }
}

[Collection("Live")]
public class Gemini4ProbeLiveTests(GeminiFixture fixture, ITestOutputHelper output)
{
    private const string Hydrogen = "gemini-4-hydrogen";
    private const string Carbon = "gemini-4-carbon";
    private const string Argon = "gemini-4-argon";
    private const string Ok = "Reply with the single word OK.";

    private static readonly Uri Base = new("https://generativelanguage.googleapis.com/v1beta/");

    private IGeminiService Gemini => fixture.Get<IGeminiService>();

    [RequiresProbeOptIn]
    public async Task Probe1_EachModel_AnswersThroughTheLibrary()
    {
        foreach (var model in new[] { Hydrogen, Carbon, Argon })
        {
            var response = await Gemini.GenerateAsync(Ok, new GeminiRequestOptions { Model = model, ThinkingLevel = "low" });
            var u = response.Usage;
            response.TryGetText(out var text);
            output.WriteLine(
                $"[{model}] text='{text?.Trim()}' finish={response.FinishReason} modelVersion={response.ModelVersion} " +
                $"prompt={u?.PromptTokenCount} candidates={u?.CandidatesTokenCount} thoughts={u?.ThoughtsTokenCount} " +
                $"cached={u?.CachedContentTokenCount} total={u?.TotalTokenCount}");
        }
    }

    [RequiresProbeOptIn]
    public async Task Probe2_OutputCap_NonStreaming()
    {
        // Google's prose says a non-streaming call to Carbon or Argon with maxOutputTokens above 32,768
        // and no continuation token returns HTTP 400. Google's own REST example sends 262,144 on hop 1.
        // The prompt is trivial, so a 200 here costs almost nothing.
        foreach (var (model, max) in new[] { (Carbon, 32768), (Carbon, 40000), (Carbon, 262144), (Argon, 40000), (Hydrogen, 40000), (Hydrogen, 100000) })
        {
            var (status, json, raw) = await PostAsync($"models/{model}:generateContent", Request(Ok, max, "low"));
            Report($"cap {model} max={max}", status, json, raw);
        }
    }

    [RequiresProbeOptIn]
    public async Task Probe3_ThinkingAndSamplingFields()
    {
        var cases = new List<(string Label, string Model, Action<JsonObject> Tweak)>
        {
            ("minimal on Hydrogen", Hydrogen, r => Level(r, "minimal")),
            ("minimal on Carbon", Carbon, r => Level(r, "minimal")),
            ("xhigh on Carbon", Carbon, r => Level(r, "xhigh")),
            ("xhigh on Argon", Argon, r => Level(r, "xhigh")),
            ("xhigh on Hydrogen", Hydrogen, r => Level(r, "xhigh")),
            ("thinkingBudget with thinkingLevel on Carbon", Carbon, r => r["generationConfig"]!["thinkingConfig"]!["thinkingBudget"] = 1024),
            ("temperature, topP, topK on Carbon", Carbon, r =>
            {
                var g = r["generationConfig"]!.AsObject();
                g["temperature"] = 0.5;
                g["topP"] = 0.8;
                g["topK"] = 40;
            }),
        };

        foreach (var (label, model, tweak) in cases)
        {
            var request = Request(Ok, 4000, "low");
            tweak(request);
            var (status, json, raw) = await PostAsync($"models/{model}:generateContent", request);
            Report(label, status, json, raw);
        }

        static void Level(JsonObject r, string level) => r["generationConfig"]!["thinkingConfig"]!["thinkingLevel"] = level;
    }

    [RequiresProbeOptIn]
    public async Task Probe4_ExplicitCaching_IsRefused()
    {
        foreach (var model in new[] { Hydrogen, Carbon })
        {
            var body = new JsonObject
            {
                ["model"] = $"models/{model}",
                ["contents"] = new JsonArray { new JsonObject { ["role"] = "user", ["parts"] = new JsonArray { new JsonObject { ["text"] = "hello" } } } },
                ["ttl"] = "60s",
            };
            var (status, json, raw) = await PostAsync("cachedContents", body);
            Report($"cachedContents create for {model}", status, json, raw);
        }
    }

    [RequiresProbeOptIn]
    public async Task Probe5_Continuation_OnCarbon()
    {
        // A long, dull task that cannot finish inside one 32K hop: 20,000 numbers in words is far above
        // 70,000 tokens, so the run ends on the total cap, after at least two CONTINUATION hops.
        const string prompt = "Write the integers from 1 to 20000 in English words, one per line, starting at one. " +
                              "Do not stop early, do not summarize and do not skip any.";
        const int max = 70000;
        string? token = null;
        long prompt_ = 0, cached = 0, candidates = 0, thoughts = 0, total = 0;

        for (var hop = 1; hop <= 4; hop++)
        {
            var request = Request(prompt, max, "low");
            if (token is not null)
            {
                request["continuationToken"] = token;
            }

            var (status, json, raw) = await PostAsync($"models/{Carbon}:generateContent", request);
            Report($"hop {hop} (token sent: {token is not null})", status, json, raw);
            if (status != HttpStatusCode.OK || json is null)
            {
                break;
            }

            var u = json["usageMetadata"];
            prompt_ += (long?)u?["promptTokenCount"] ?? 0;
            cached += (long?)u?["cachedContentTokenCount"] ?? 0;
            candidates += (long?)u?["candidatesTokenCount"] ?? 0;
            thoughts += (long?)u?["thoughtsTokenCount"] ?? 0;
            total += (long?)u?["totalTokenCount"] ?? 0;

            var candidate = json["candidates"]?[0];
            token = (string?)candidate?["continuationToken"];
            if ((string?)candidate?["finishReason"] != "CONTINUATION" || token is null)
            {
                break;
            }
        }

        output.WriteLine($"SUM OF HOPS: prompt={prompt_} cached={cached} candidates={candidates} thoughts={thoughts} total={total}");
    }

    // ----- helpers -----

    private static JsonObject Request(string prompt, int? maxOutputTokens, string thinkingLevel)
    {
        var generationConfig = new JsonObject { ["thinkingConfig"] = new JsonObject { ["thinkingLevel"] = thinkingLevel } };
        if (maxOutputTokens is not null)
        {
            generationConfig["maxOutputTokens"] = maxOutputTokens;
        }

        return new JsonObject
        {
            ["contents"] = new JsonArray { new JsonObject { ["role"] = "user", ["parts"] = new JsonArray { new JsonObject { ["text"] = prompt } } } },
            ["generationConfig"] = generationConfig,
        };
    }

    private async Task<(HttpStatusCode Status, JsonNode? Json, string Raw)> PostAsync(string path, JsonObject body)
    {
        using var http = new HttpClient { BaseAddress = Base, Timeout = TimeSpan.FromMinutes(20) };
        http.DefaultRequestHeaders.Add("x-goog-api-key", Environment.GetEnvironmentVariable("GeminiApiKey"));
        using var response = await http.PostAsync(path, new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"));
        var raw = await response.Content.ReadAsStringAsync();
        JsonNode? json = null;
        try
        {
            json = JsonNode.Parse(raw);
        }
        catch (System.Text.Json.JsonException)
        {
            // Leave json null. The raw text is printed.
        }

        return (response.StatusCode, json, raw);
    }

    private void Report(string label, HttpStatusCode status, JsonNode? json, string raw)
    {
        var line = new StringBuilder($"[{label}] HTTP {(int)status}");
        if (status == HttpStatusCode.OK && json is not null)
        {
            var candidate = json["candidates"]?[0];
            var text = string.Concat(candidate?["content"]?["parts"]?.AsArray()
                .Select(p => (string?)p?["text"]) ?? []);
            var u = json["usageMetadata"];
            line.Append($" finish={candidate?["finishReason"]} token={(candidate?["continuationToken"] is null ? "no" : "yes")} ")
                .Append($"textChars={text.Length} head='{Head(text, 40)}' modelVersion={json["modelVersion"]} ")
                .Append($"prompt={u?["promptTokenCount"]} cached={u?["cachedContentTokenCount"]} candidates={u?["candidatesTokenCount"]} ")
                .Append($"thoughts={u?["thoughtsTokenCount"]} total={u?["totalTokenCount"]}");
        }
        else
        {
            line.Append(' ').Append(Head(json?["error"]?["message"]?.ToString() ?? raw, 400));
        }

        output.WriteLine(line.ToString());
    }

    private static string Head(string text, int length) =>
        (text.Length <= length ? text : text[..length]).Replace("\n", "\\n").Replace("\r", "");
}
