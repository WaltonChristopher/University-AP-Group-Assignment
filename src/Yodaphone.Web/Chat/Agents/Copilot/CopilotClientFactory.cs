using GitHub.Copilot;

namespace Yodaphone.Web.Chat.Agents.Copilot;

/// <summary>
/// Adapts the real SDK client and sessions to the operations used by the agent.
/// </summary>
/// <remarks>
/// An adapter wraps an SDK object and forwards calls through our interface. These wrappers
/// leave configuration, response validation, and lifetime management with GitHubCopilotAgent.
/// They do not catch SDK exceptions or translate them into user-facing messages.
/// </remarks>
public sealed class CopilotClientFactory : ICopilotClientFactory
{
    public ICopilotClient Create(CopilotClientOptions options)
    {
        // Construction does not start the client; the agent explicitly awaits StartAsync.
        return new ClientAdapter(new CopilotClient(options));
    }

    // Wraps the SDK client so tests can replace its operations without starting a Copilot runtime.
    private sealed class ClientAdapter(CopilotClient client) : ICopilotClient
    {
        public Task StartAsync(CancellationToken cancellationToken)
        {
            return client.StartAsync(cancellationToken);
        }

        public async Task<ICopilotSession> CreateSessionAsync(
            SessionConfig config,
            CancellationToken cancellationToken)
        {
            var session = await client.CreateSessionAsync(config, cancellationToken);
            // Wrap the returned session too, so tests can replace sending and session cleanup.
            return new SessionAdapter(session);
        }

        public ValueTask DisposeAsync()
        {
            // Forward cleanup to the SDK; disposing only the wrapper would leave resources open.
            return client.DisposeAsync();
        }
    }

    private sealed class SessionAdapter(CopilotSession session) : ICopilotSession
    {
        public Task<AssistantMessageEvent?> SendAndWaitAsync(
            MessageOptions options,
            CancellationToken cancellationToken)
        {
            return session.SendAndWaitAsync(options, cancellationToken: cancellationToken);
        }

        public ValueTask DisposeAsync()
        {
            return session.DisposeAsync();
        }
    }
}
