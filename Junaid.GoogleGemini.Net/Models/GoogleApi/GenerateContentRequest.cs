namespace Junaid.GoogleGemini.Net.Models.GoogleApi
{
    public class GenerateContentRequest
    {
        public Content[] contents { get; set; }
    }

    public class GenerateContentRequestWithConfiguration : GenerateContentRequest
    {
        public List<object> safetySettings { get; set; }
        public GenerationConfig generationConfig { get; set; }
    }
}