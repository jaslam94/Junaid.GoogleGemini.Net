namespace Junaid.GoogleGemini.Net.Models.GoogleApi
{
    public class GenerateContentRequest
    {
        public Content[] contents { get; set; }
        public List<object> safetySettings { get; set; }
        public GenerationConfig generationConfig { get; set; }

        public GenerateContentRequest(Content[] contents)
        {
            this.contents = contents;
        }

        public GenerateContentRequest(Content[] contents, List<object> safetySettings, GenerationConfig generationConfig)
        {
            this.contents = contents;
            this.safetySettings = safetySettings;
            this.generationConfig = generationConfig;
        }

        public void ApplyConfiguration(GenerateContentConfiguration configuration)
        {
            if (configuration.safetySettings != null)
            {
                this.safetySettings = new List<object>();
                foreach (var safetySetting in configuration.safetySettings)
                {
                    this.safetySettings.Add(
                        new
                        {
                            safetySetting.category,
                            safetySetting.threshold
                        });
                }
            }

            this.generationConfig = configuration.generationConfig;
        }
    }
}