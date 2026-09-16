namespace Yodaphone.Web.Domain;

public sealed class Conversation
{
    private readonly List<ChatMessage> messages = [];

    public Guid Id { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset LastActivityAt { get; private set; }
    public ConversationStatus Status { get; private set; }
    public IReadOnlyCollection<ChatMessage> Messages => messages.AsReadOnly();
    // Parameterless constructor kept for EF Core (issue #8).
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

    public ChatMessage AddMessage(MessageRole role, string content)
    {
        if (Status == ConversationStatus.Closed)
        {
            throw new InvalidOperationException("Cannot add a message to a closed conversation.");
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