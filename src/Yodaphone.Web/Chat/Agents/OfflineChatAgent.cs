using Yodaphone.Web.Domain;

namespace Yodaphone.Web.Chat.Agents;

/// <summary>
/// An implementation of ChatAgentBase that provides offline responses.
/// </summary>
public sealed class OfflineChatAgent : ChatAgentBase
{
    // Fictional UK landline from Ofcom's range reserved for drama.
    private const string UnavailableResponse =
        "The chat service is currently unavailable, so we cannot complete your request here. " +
        "Please call YodaPhone on 01632 960123 for direct support. " + "\n\n" +
        "You will be notified if the chat service is restored and available again. " +
        "Thank you for your patience.";

    protected override Task<AgentChatResponse> GetReplyCoreAsync(
        IReadOnlyList<ChatMessage> history,
        CancellationToken cancellationToken
    )
    {
        var response = new AgentChatResponse
        {
            Content = UnavailableResponse
        };

        return Task.FromResult(response);
    }
}
