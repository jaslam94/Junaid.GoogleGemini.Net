namespace Junaid.GoogleGemini.Net.Models.GoogleApi
{
    public class GenerateContentRequest : GenerateContentConfiguration
    {
        public Content[] contents { get; set; }

        public GenerateContentRequest(Content[] contents)
        {
            this.contents = contents;
        }

        public void ApplyConfiguration(GenerateContentConfiguration configuration)
        {
            this.safetySettings = configuration.safetySettings;
            this.generationConfig = configuration.generationConfig;
        }
    }
}