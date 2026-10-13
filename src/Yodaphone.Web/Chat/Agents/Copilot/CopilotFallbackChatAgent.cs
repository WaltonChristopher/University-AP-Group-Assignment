using Microsoft.Extensions.Options;
using Yodaphone.Web.Domain;

namespace Yodaphone.Web.Chat.Agents.Copilot;

/// <summary>
/// Uses Copilot until a reply fails, then stays offline for this scoped chat session.
/// </summary>
public sealed class CopilotFallbackChatAgent : ChatAgentBase
{
    private readonly GitHubCopilotAgent copilotAgent;
    private readonly OfflineChatAgent offlineAgent;
    private readonly TimeSpan responseTimeout;
    private readonly ILogger<CopilotFallbackChatAgent> logger;
    private bool offlineMode;

    public CopilotFallbackChatAgent(
        GitHubCopilotAgent copilotAgent,
        OfflineChatAgent offlineAgent,
        IOptions<CopilotOptions> options,
        ILogger<CopilotFallbackChatAgent> logger)
    {
        ArgumentNullException.ThrowIfNull(copilotAgent);
        ArgumentNullException.ThrowIfNull(offlineAgent);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        this.copilotAgent = copilotAgent;
        this.offlineAgent = offlineAgent;
        responseTimeout = TimeSpan.FromSeconds(options.Value.ResponseTimeoutSeconds);
        this.logger = logger;
    }

    protected override async Task<AgentChatResponse> GetReplyCoreAsync(
        IReadOnlyList<ChatMessage> history,
        CancellationToken cancellationToken)
    {
        if (!offlineMode)
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
                offlineMode = true;
                logger.LogWarning(
                    "Copilot could not complete a reply ({FailureType}); switching this chat session offline.",
                    exception.GetType().Name);
            }
        }

        return await offlineAgent.GetReplyAsync(history, cancellationToken);
    }
}
