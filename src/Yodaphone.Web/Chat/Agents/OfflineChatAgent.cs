using Yodaphone.Web.Domain;

namespace Yodaphone.Web.Chat.Agents;

/// <summary>
/// An implementation of ChatAgentBase that provides offline responses.
/// </summary>
/// <TODO>
/// This class is a placeholder for an offline chat agent. It should be extended to provide better offline responses.
/// </TODO>
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
