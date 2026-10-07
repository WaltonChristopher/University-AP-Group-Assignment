namespace Yodaphone.Web.Domain;

public sealed class Conversation
{
    private readonly List<ChatMessage> messages = [];

    public Guid Id { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset LastActivityAt { get; private set; }
    public ConversationStatus Status { get; private set; }
    public IReadOnlyCollection<ChatMessage> Messages => messages.AsReadOnly();
    // Used by Restore to recreate a saved conversation with its original state.
    private Conversation()
    {
    }
    public Conversation(Guid id, DateTimeOffset startedAt)
    {
        Id = id;
        StartedAt = startedAt;
        LastActivityAt = startedAt;
        Status = ConversationStatus.Active;
    }

    /// <summary>
    /// Recreates a conversation previously stored by a repository.
    /// </summary>
    public static Conversation Restore(
        Guid id,
        DateTimeOffset startedAt,
        DateTimeOffset lastActivityAt,
        ConversationStatus status,
        IEnumerable<ChatMessage> restoredMessages)
    {
        ArgumentNullException.ThrowIfNull(restoredMessages);

        var conversation = new Conversation
        {
            Id = id,
            StartedAt = startedAt,
            LastActivityAt = lastActivityAt,
            Status = status
        };

        foreach (var message in restoredMessages.OrderBy(message => message.SentAt))
        {
            if (message.ConversationId != id)
            {
                throw new ArgumentException("A restored message belongs to a different conversation.", nameof(restoredMessages));
            }

            conversation.messages.Add(message);
        }

        return conversation;
    }

    public ChatMessage AddMessage(MessageRole role, string content)
    {
        if (Status == ConversationStatus.Closed)
        {
            throw new InvalidOperationException("Cannot add a message to a closed conversation.");
        }

        if (role == MessageRole.Customer)
        {
            ChatContentSanitizer.EnsureSafeUserContent(content);
        }

        var message = new ChatMessage(Id, role, content);
        messages.Add(message);
        LastActivityAt = DateTimeOffset.UtcNow;

        return message;
    }

    public void Close()
    {
        Status = ConversationStatus.Closed;
    }
}
