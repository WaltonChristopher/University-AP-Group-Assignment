namespace Yodaphone.Web.Data.Entities;

public class Message
{
    public int MessageId { get; set; }

    // Stable public/domain identifier. MessageId remains the SQLite key.
    public Guid Id { get; set; }

    public string MessageContent { get; set; } = string.Empty;

    public DateTime DateTime { get; set; }

    public string Status { get; set; } = string.Empty;

    public int SenderId { get; set; }

    public int ConversationId { get; set; }

    public User Sender { get; set; } = null!;

    public Conversation Conversation { get; set; } = null!;
}
