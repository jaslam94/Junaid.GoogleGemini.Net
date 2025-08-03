namespace Junaid.GoogleGemini.Net.Models.GoogleApi;

public class GenerateContentResponse
{
    public Candidate[] candidates { get; set; }
    public Promptfeedback promptFeedback { get; set; }

    /// <summary>
    /// Extracts the text content from the response, handling blocked content gracefully
    /// </summary>
    /// <returns>The generated text or a message indicating the content was blocked</returns>
    public string Text()
    {
        // Check if we have any candidates
        if (candidates == null || candidates.Length == 0)
        {
            return "[No content generated - response contained no candidates]";
        }

        // Get the first candidate
        var firstCandidate = candidates[0];
        
        // Check for content blocking
        if (firstCandidate?.content?.Parts == null || firstCandidate.content.Parts.Count == 0)
        {
            return "[Content was blocked by safety filters]";
        }

        // Try to get the text from the first part
        var text = firstCandidate.content.Parts[0]?.Text;
        
        if (string.IsNullOrEmpty(text))
        {
            return "[Content was blocked or empty]";
        }

        return text;
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