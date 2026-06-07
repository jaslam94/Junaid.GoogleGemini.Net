namespace Junaid.GoogleGemini.Net.Models.Requests
{
    /// <summary>
    /// A single chat message with a validated role ("user" or "model") and non-empty text.
    /// Immutable by design — validate once at construction, then it can't drift into an invalid state.
    /// </summary>
    public class MessageObject
    {
        /// <summary>The message author: "user" or "model".</summary>
        public string Role { get; }

        /// <summary>The message text.</summary>
        public string Text { get; }

        /// <summary>Creates a message, validating the role and text up front.</summary>
        /// <param name="role">"user" or "model".</param>
        /// <param name="text">Non-empty message text.</param>
        public MessageObject(string role, string text)
        {
            Role = role is "model" or "user"
                ? role
                : throw new ArgumentException("Value cannot be other than 'model' or 'user'.", nameof(role));

            Text = !string.IsNullOrWhiteSpace(text)
                ? text
                : throw new ArgumentException("Text cannot be empty.", nameof(text));
        }
    }
}
