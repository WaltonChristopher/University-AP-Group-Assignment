using GitHub.Copilot;

namespace Yodaphone.Web.Chat.Agents.Copilot;

/// <summary>
/// The client operations used by the agent, replaceable without starting a Copilot runtime.
/// </summary>
/// <remarks>
/// This is deliberately a small subset of the SDK. IAsyncDisposable lets the agent use
/// await using for cleanup with either a real client or a test double.
/// </remarks>
public interface ICopilotClient : IAsyncDisposable
{
    /// <summary>
    /// Starts the connection before the agent requests a session.
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Creates a session that the caller owns and disposes before disposing the client.
    /// </summary>
    Task<ICopilotSession> CreateSessionAsync(SessionConfig config, CancellationToken cancellationToken);
}
