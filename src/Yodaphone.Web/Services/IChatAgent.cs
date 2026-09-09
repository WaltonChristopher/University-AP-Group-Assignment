/// <summary>
/// Defines the interface for a chat agent that can provide responses in a conversation.
/// </summary>
public interface IChatAgent
{
    Task<AgentChatResponse> GetReplyAsync(
        IReadOnlyList<ChatMessage> message,
        CancellationToken cancellationToken
    );
}

