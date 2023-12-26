namespace Junaid.GoogleGemini.Net.Models.GoogleApi
{
    public class GenerateContentRequest
    {
        public Content[] contents { get; set; }
        public List<object> safetySettings { get; set; }
        public GenerationConfig generationConfig { get; set; }
    }
}