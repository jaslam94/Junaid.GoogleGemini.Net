namespace Junaid.GoogleGemini.Net.Models.GoogleApi
{
    public class GenerateContentResponse
    {
        public Candidate[] candidates { get; set; }
        public Promptfeedback promptFeedback { get; set; }

        public string Text()
        {
            return this.candidates?[0].content?.parts?[0]?.text;
        }
    }

    public class Promptfeedback
    {
        public Safetyrating[] safetyRatings { get; set; }
    }

    public class Safetyrating
    {
        public string category { get; set; }
        public string probability { get; set; }
    }

    public class Candidate
    {
        public Content content { get; set; }
        public string finishReason { get; set; }
        public int index { get; set; }
        public Safetyrating1[] safetyRatings { get; set; }
    }

    public class Safetyrating1
    {
        public string category { get; set; }
        public string probability { get; set; }
    }
}