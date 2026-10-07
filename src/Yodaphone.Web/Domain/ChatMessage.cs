namespace Yodaphone.Web.Domain;

public sealed class ChatMessage
{
    public Guid Id { get; private set; }
    public Guid ConversationId { get; private set; }
    public MessageRole Role { get; private set; }
    public string Content { get; private set; }
    public DateTimeOffset SentAt { get; private set; }

    // Used by Restore to recreate a saved message with its original ID and timestamp.
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

    /// <summary>
    /// Recreates a message previously stored by a repository.
    /// </summary>
    public static ChatMessage Restore(
        Guid id,
        Guid conversationId,
        MessageRole role,
        string content,
        DateTimeOffset sentAt)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("Message content cannot be empty.", nameof(content));
        }

        return new ChatMessage
        {
            Id = id,
            ConversationId = conversationId,
            Role = role,
            Content = content,
            SentAt = sentAt
        };
    }

    public bool IsFromAgent => Role == MessageRole.Assistant;

}
