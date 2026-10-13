using Yodaphone.Web.Domain;

namespace Yodaphone.Web.Chat.Agents;

/// <summary>
/// Validates chat requests and replies, replacing blocked replies with a canned explanation.
/// </summary>
public abstract class ChatAgentBase : IChatAgent
{
    private const string BlockedResponse =
        "Sorry, I can't assist with that." +
        "Please contact the support team on 01632 960123 or ask something else.";

    /// <summary>
    /// Gets a reply for the given chat history.
    /// </summary>
    /// <param name="history">The chat history.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The agent's reply.</returns>
    public async Task<AgentChatResponse> GetReplyAsync(
        IReadOnlyList<ChatMessage> history,
        CancellationToken cancellationToken)
    {
        ChatAgentRequestValidator.Validate(history, cancellationToken);

        var response = await GetReplyCoreAsync(history, cancellationToken);
        if (response is null || string.IsNullOrWhiteSpace(response.Content))
        {
            throw new InvalidOperationException("The agent response was empty or null.");
        }

        try
        {
            ChatContentSanitizer.EnsureSafeAgentResponse(response.Content);
        }
        catch (InvalidOperationException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // A blocked reply is handled here so the Copilot fallback still receives
            // a valid response and keeps the session online. Discard the original content.
            return new AgentChatResponse { Content = BlockedResponse };
        }

        return response;
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
