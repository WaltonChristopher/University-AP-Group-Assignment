namespace Yodaphone.Web.Chat.Agents;

/// <summary>
/// Optional connection recovery for an agent that temporarily falls back to offline replies.
/// </summary>
public interface IRecoverableChatAgent
{
    TimeSpan ConnectionRetryInterval { get; }

    /// <summary>
    /// Checks availability without sending chat messages. Returns true only when switching online.
    /// </summary>
    /// <remarks>
    /// The intention here is to implement TryRestoreConnectionAsync in a way that does not request an
    /// agent response, thus avoiding using up credits in checking for availability. The agent may still
    /// be unavailable after this check, but the caller can use this to determine whether to switch back online.
    /// </remarks>
    Task<bool> TryRestoreConnectionAsync(CancellationToken cancellationToken);
}
