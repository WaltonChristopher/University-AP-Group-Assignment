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

    private static readonly string[] CannedResponses =
    {
        "Thank you for your inquiry. A YodaPhone specialist will be with you shortly.",
        "I understand. Let me look into your account and get back to you.",
        "That is a great question. Here is what I found for you.",
        "Your request has been logged. Reference number attached to this conversation.",
        "I can help with that. Could you tell me a little more about the issue?"
    };

    protected override Task<AgentChatResponse> GetReplyCoreAsync(
        IReadOnlyList<ChatMessage> history,
        CancellationToken cancellationToken
    )
    {
        var response = new AgentChatResponse
        {
            Content = CannedResponses[Random.Shared.Next(CannedResponses.Length)]
        };

        return Task.FromResult(response);
    }
}
