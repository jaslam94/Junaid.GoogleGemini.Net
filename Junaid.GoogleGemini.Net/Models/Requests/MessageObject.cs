namespace Junaid.GoogleGemini.Net.Models.Requests
{
    public class MessageObject
    {
        private string role;
        private string text;

        public string Role
        {
            get => role;
            private set => role = value is "model" or "user"
                ? value
                : throw new ArgumentException("Value cannot be other than 'model' or 'user'.");
        }

        public string Text
        {
            get => text;
            private set => text = !string.IsNullOrWhiteSpace(value)
                ? value
                : throw new ArgumentException("Text cannot be empty.");
        }

        public MessageObject(string role, string text)
        {
            Role = role;
            Text = text;
        }
    }
}