namespace Junaid.GoogleGemini.Net.Models.GoogleApi
{
    public class ApiErrorResponse
    {
        public Error error { get; set; }
    }

    public class Error
    {
        public int code { get; set; }
        public string message { get; set; }
        public string status { get; set; }
    }
}