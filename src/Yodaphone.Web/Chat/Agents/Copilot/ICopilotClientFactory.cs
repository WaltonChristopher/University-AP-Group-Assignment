using GitHub.Copilot;

namespace Yodaphone.Web.Chat.Agents.Copilot;

/// <summary>
/// Creates a client for one agent request. The caller owns and disposes the client.
/// </summary>
public interface ICopilotClientFactory
{
    /// <summary>
    /// Creates a fresh client. The caller must start it before creating a session.
    /// </summary>
    ICopilotClient Create(CopilotClientOptions options);
}
