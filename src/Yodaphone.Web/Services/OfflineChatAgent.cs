/// <summary>
/// An implementation of IChatAgent that provides offline responses.
/// </summary>
public class OfflineChatAgent : IChatAgent
{
    public Task<AgentChatResponse> GetReplyAsync(
        IReadOnlyList<ChatMessage> message,
        CancellationToken cancellationToken
    )
    {
        var response = new AgentChatResponse
        {
            Content = "This is an offline response."
        };

        return Task.FromResult(response);
    }
}