namespace Junaid.GoogleGemini.Net.Models.GoogleApi
{
    public class GenerateContentConfiguration
    {
        public SafetySetting[] safetySettings { get; set; }
        public GenerationConfig generationConfig { get; set; }
    }

    public class SafetySetting
    {
        public string category { get; set; }
        public string threshold { get; set; }
    }

    public class GenerationConfig
    {
        public List<string> stopSequences { get; set; }
        public double temperature { get; set; }
        public int maxOutputTokens { get; set; }
        public double topP { get; set; }
        public int topK { get; set; }
    }
}