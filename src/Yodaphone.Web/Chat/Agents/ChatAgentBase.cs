using Yodaphone.Web.Chat;
using Yodaphone.Web.Domain;

namespace Yodaphone.Web.Chat.Agents;

/// <summary>
/// Provides request validation shared by chat-agent implementations.
/// </summary>
public abstract class ChatAgentBase : IChatAgent
{
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
    protected abstract Task<AgentChatResponse> GetReplyCoreAsync(
        IReadOnlyList<ChatMessage> history,
        CancellationToken cancellationToken);
}
