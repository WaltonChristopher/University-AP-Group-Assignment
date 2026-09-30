using GitHub.Copilot;
using Microsoft.Extensions.Options;

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
    private readonly string? gitHubToken;

    /// <summary>
    /// Creates a factory using the server-side Copilot configuration.
    /// </summary>
    public CopilotClientFactory(IOptions<CopilotOptions> options)
        : this(options?.Value.GitHubToken)
    {
    }

    /// <summary>
    /// Creates a factory that uses any locally persisted Copilot or GitHub CLI login.
    /// </summary>
    public CopilotClientFactory()
        : this((string?)null)
    {
    }

    internal CopilotClientFactory(string? gitHubToken)
    {
        this.gitHubToken = string.IsNullOrWhiteSpace(gitHubToken) ? null : gitHubToken;
    }

    public ICopilotClient Create(CopilotClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        // This credential is applied inside the server process, never forwarded from the
        // browser. When no token is configured, retain the SDK's local-login behaviour for
        // developer machines (for example, a previously authenticated GitHub CLI).
        options.GitHubToken = gitHubToken;
        options.UseLoggedInUser = gitHubToken is null;

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
