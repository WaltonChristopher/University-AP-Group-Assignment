using GitHub.Copilot;
using Microsoft.Extensions.Options;

namespace Yodaphone.Web.Chat.Agents.Copilot;

/// <summary>
/// Shares a throttled, non-inference availability check across chat sessions on this server.
/// </summary>
public sealed class CopilotConnectionHealthCheck(
    ICopilotClientFactory clientFactory,
    IOptions<CopilotOptions> options,
    ILogger<CopilotConnectionHealthCheck> logger,
    TimeProvider timeProvider)
{
    private readonly SemaphoreSlim checkLock = new(1, 1);
    private readonly TimeSpan checkInterval = TimeSpan.FromSeconds(options.Value.ResponseTimeoutSeconds);
    private DateTimeOffset nextCheckAt = DateTimeOffset.MinValue;
    private bool available;
    private Task<bool>? pendingCheck;

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken)
    {
        await checkLock.WaitAsync(cancellationToken);
        try
        {
            if (timeProvider.GetUtcNow() < nextCheckAt)
            {
                return available;
            }

            // A timed-out SDK operation may still be cleaning up. Never accumulate overlapping clients.
            if (pendingCheck is { IsCompleted: false })
            {
                return false;
            }

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(checkInterval);
            try
            {
                // Bound startup, the metadata request, and disposal even if the SDK ignores cancellation.
                pendingCheck = CheckModelsAsync(timeout.Token);
                available = await pendingCheck.WaitAsync(timeout.Token);
                cancellationToken.ThrowIfCancellationRequested();
            }
            catch (Exception exception)
            {
                cancellationToken.ThrowIfCancellationRequested();
                available = false;
                logger.LogDebug("Copilot availability check failed ({FailureType}).", exception.GetType().Name);
            }

            // Cache both success and failure to avoid a check for every browser session.
            nextCheckAt = timeProvider.GetUtcNow() + checkInterval;
            return available;
        }
        finally
        {
            checkLock.Release();
        }
    }

    private async Task<bool> CheckModelsAsync(CancellationToken cancellationToken)
    {
        // A fresh client avoids the SDK's per-client model-list cache. No chat session is created.
        await using var client = clientFactory.Create(new CopilotClientOptions
        {
            BaseDirectory = Path.Combine(Path.GetTempPath(), "Yodaphone-Copilot", "health checks"),
            Mode = CopilotClientMode.Empty
        });
        await client.StartAsync(cancellationToken);
        var models = await client.ListModelsAsync(cancellationToken);
        return models.Count > 0;
    }
}
