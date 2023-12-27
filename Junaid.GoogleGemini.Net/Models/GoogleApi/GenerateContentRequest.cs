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
            if (configuration.safetySettings != null)
            {
                foreach (var safetySetting in configuration.safetySettings)
                {
                    this.safetySettings.Append(
                        new SafetySetting
                        {
                            category = safetySetting.category,
                            threshold = safetySetting.threshold
                        });
                }
            }

            this.generationConfig = configuration.generationConfig;
        }
    }
}