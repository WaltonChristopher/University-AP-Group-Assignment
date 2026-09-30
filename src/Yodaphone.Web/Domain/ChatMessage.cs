namespace Yodaphone.Web.Domain;

public sealed class ChatMessage
{
    public Guid Id { get; private set; }
    public Guid ConversationId { get; private set; }
    public MessageRole Role { get; private set; }
    public string Content { get; private set; }
    public DateTimeOffset SentAt { get; private set; }

    // Parameterless constructor kept for EF Core (issue #8) — not for app code to use.
    private ChatMessage()
    {
        Content = string.Empty;
    }

    internal ChatMessage(Guid conversationId, MessageRole role, string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("Message content cannot be empty.", nameof(content));
        }

        Id = Guid.NewGuid();
        ConversationId = conversationId;
        Role = role;
        Content = content;
        SentAt = DateTimeOffset.UtcNow;
    }
}