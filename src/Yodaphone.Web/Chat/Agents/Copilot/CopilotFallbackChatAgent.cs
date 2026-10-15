using Microsoft.Extensions.Options;
using Yodaphone.Web.Domain;

namespace Yodaphone.Web.Chat.Agents.Copilot;

/// <summary>
/// Falls back to offline replies after failures and restores Copilot after a successful health check.
/// </summary>
public sealed class CopilotFallbackChatAgent : ChatAgentBase, IRecoverableChatAgent
{
    private readonly GitHubCopilotAgent copilotAgent;
    private readonly OfflineChatAgent offlineAgent;
    private readonly TimeSpan responseTimeout;
    private readonly ILogger<CopilotFallbackChatAgent> logger;
    private readonly CopilotConnectionHealthCheck connectionHealthCheck;
    private int offlineMode;

    public TimeSpan ConnectionRetryInterval => responseTimeout;

    /// <summary>
    /// Creates a chat agent that uses Copilot when available and falls back to offline replies after failures.
    /// </summary>
    /// <param name="copilotAgent">The Copilot agent to use when available.</param>
    /// <param name="offlineAgent">The offline agent to use when Copilot is unavailable.</param>
    /// <param name="options">The options for the Copilot agent.</param>
    /// <param name="logger">The logger to use for logging messages.</param>
    /// <param name="connectionHealthCheck">The health check to use for checking the connection to Copilot.</param>
    public CopilotFallbackChatAgent(
        GitHubCopilotAgent copilotAgent,
        OfflineChatAgent offlineAgent,
        IOptions<CopilotOptions> options,
        ILogger<CopilotFallbackChatAgent> logger,
        CopilotConnectionHealthCheck connectionHealthCheck)
    {
        ArgumentNullException.ThrowIfNull(copilotAgent);
        ArgumentNullException.ThrowIfNull(offlineAgent);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(connectionHealthCheck);
        this.copilotAgent = copilotAgent;
        this.offlineAgent = offlineAgent;
        responseTimeout = TimeSpan.FromSeconds(options.Value.ResponseTimeoutSeconds);
        this.logger = logger;
        this.connectionHealthCheck = connectionHealthCheck;
    }

    public async Task<bool> TryRestoreConnectionAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Volatile.Read(ref offlineMode) == 0 ||
            !await connectionHealthCheck.IsAvailableAsync(cancellationToken))
        {
            return false;
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (Interlocked.CompareExchange(ref offlineMode, 0, 1) != 1)
        {
            return false;
        }

        logger.LogInformation("Copilot connection restored; switching this chat session online.");
        return true;
    }

    protected override async Task<AgentChatResponse> GetReplyCoreAsync(
        IReadOnlyList<ChatMessage> history,
        CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref offlineMode) == 0)
        {
            using var requestCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            requestCancellation.CancelAfter(responseTimeout);

            try
            {
                // Bound the whole workflow, including startup and disposal, even if the
                // SDK does not promptly honour cancellation. Late replies are discarded.
                var response = await copilotAgent.GetReplyAsync(history, requestCancellation.Token)
                    .WaitAsync(requestCancellation.Token);
                cancellationToken.ThrowIfCancellationRequested();
                return response;
            }
            catch (Exception exception)
            {
                // Closing the page or cancelling a request must not switch the session offline.
                cancellationToken.ThrowIfCancellationRequested();
                Interlocked.Exchange(ref offlineMode, 1);
                logger.LogWarning(
                    "Copilot could not complete a reply ({FailureType}); switching this chat session offline.",
                    exception.GetType().Name);
            }
        }

        return await offlineAgent.GetReplyAsync(history, cancellationToken);
    }
}
