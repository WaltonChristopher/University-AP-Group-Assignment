using GitHub.Copilot;

namespace Yodaphone.Web.Chat.Agents.Copilot;

/// <summary>
/// The session operation used by the agent. The caller owns and disposes the session.
/// </summary>
public interface ICopilotSession : IAsyncDisposable
{
    /// <summary>
    /// Sends a prompt and waits for the final reply. A null result means no reply was received.
    /// The agent is responsible for rejecting that result or empty content.
    /// </summary>
    Task<AssistantMessageEvent?> SendAndWaitAsync(MessageOptions options, CancellationToken cancellationToken);
}
