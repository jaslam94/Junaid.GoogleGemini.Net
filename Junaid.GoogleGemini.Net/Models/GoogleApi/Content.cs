namespace Junaid.GoogleGemini.Net.Models.GoogleApi
{
    public class Content
    {
        public Part[] parts { get; set; }
        public string role { get; set; }
    }

    public class Part
    {
        public string text { get; set; }
        public Inline_Data inline_data { get; set; }
    }
}