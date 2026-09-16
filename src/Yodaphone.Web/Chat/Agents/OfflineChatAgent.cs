using Yodaphone.Web.Chat;
using Yodaphone.Web.Domain;

namespace Yodaphone.Web.Chat.Agents;

/// <summary>
/// An implementation of IChatAgent that provides offline responses.
/// </summary>
public sealed class OfflineChatAgent : ChatAgentBase
{
    protected override Task<AgentChatResponse> GetReplyCoreAsync(
        IReadOnlyList<ChatMessage> history,
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
