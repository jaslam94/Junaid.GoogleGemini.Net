namespace Junaid.GoogleGemini.Net.Models.GoogleApi;

public class GenerateContentConfiguration
{
    public SafetySetting[] safetySettings { get; set; }
    public GenerationConfig generationConfig { get; set; }
}