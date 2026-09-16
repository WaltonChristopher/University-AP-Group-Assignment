using Yodaphone.Web.Domain;

namespace Yodaphone.Web.Chat.Agents;

/// <summary>
/// Base ChatAgent implementation that provides request validation before calling the core reply logic.
/// </summary>
public abstract class ChatAgentBase : IChatAgent
{
    /// <summary>
    /// Gets a reply for the given chat history.
    /// </summary>
    /// <param name="history">The chat history.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The agent's reply.</returns>
    public Task<AgentChatResponse> GetReplyAsync(
        IReadOnlyList<ChatMessage> history,
        CancellationToken cancellationToken)
    {
        ChatAgentRequestValidator.Validate(history, cancellationToken);

        return GetReplyCoreAsync(history, cancellationToken);
    }

    /// <summary>
    /// Gets a reply after the request has been validated.
    /// </summary>
    /// <param name="history">The chat history.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The agent's reply.</returns>
    protected abstract Task<AgentChatResponse> GetReplyCoreAsync(
        IReadOnlyList<ChatMessage> history,
        CancellationToken cancellationToken);
}
