using System.Net;
using Junaid.GoogleGemini.Net.Infrastructure;
using Junaid.GoogleGemini.Net.Infrastructure.Options;
using Junaid.GoogleGemini.Net.Services;
using Junaid.GoogleGemini.Net.Tests.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Junaid.GoogleGemini.Net.Tests.Services;

public class GeminiServiceStructuredOutputTests
{
    private sealed class Person
    {
        public string Name { get; set; } = "";
        public int Age { get; set; }
    }

    private static GeminiService CreateService(FakeHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/v1beta/") };
        var client = new GeminiClient(httpClient, NullLogger<GeminiClient>.Instance, GeminiRateLimiter.CreateDisabled(), GeminiCostGovernor.CreateDisabled());
        var options = Options.Create(new GeminiOptions
        {
            ApiKey = "AIzaSyDUMMY_KEY_FOR_UNIT_TESTS_12345",
            DefaultModel = "gemini-2.5-flash"
        });
        return new GeminiService(client, NullLogger<GeminiService>.Instance, options, new SafetyService(), GeminiCostGovernor.CreateDisabled());
    }

    [Fact]
    public async Task GenerateAsyncT_DeserializesJson_AndSendsSchema()
    {
        // The model's reply is a candidate whose text is the JSON object for Person.
        const string responseJson =
            """{"candidates":[{"content":{"role":"model","parts":[{"text":"{\"name\":\"Ada Lovelace\",\"age\":36}"}]},"finishReason":"STOP"}]}""";
        var handler = FakeHttpMessageHandler.RespondWith(HttpStatusCode.OK, responseJson);
        var service = CreateService(handler);

        var person = await service.GenerateAsync<Person>("Give me a famous computer scientist.");

        Assert.Equal("Ada Lovelace", person.Name);
        Assert.Equal(36, person.Age);

        // The outgoing request must constrain output to JSON and include a derived schema.
        var requestBody = handler.RequestBodies[0]!;
        Assert.Contains("\"responseMimeType\":\"application/json\"", requestBody);
        Assert.Contains("\"responseSchema\"", requestBody);
        Assert.Contains("\"properties\"", requestBody);
    }
}
