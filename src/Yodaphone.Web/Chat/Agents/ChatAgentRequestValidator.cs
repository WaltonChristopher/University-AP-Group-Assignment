using Yodaphone.Web.Domain;

namespace Yodaphone.Web.Chat.Agents;

/// <summary>
/// Provides validation for chat agent requests.
/// </summary>
internal static class ChatAgentRequestValidator
{
    /// <summary>
    /// Validates the chat history and cancellation token for a chat agent request.
    /// </summary>
    /// <param name="history"></param>
    /// <param name="cancellationToken"></param>
    /// <exception cref="ArgumentException"></exception>
    public static void Validate(
        IReadOnlyList<ChatMessage> history,
        CancellationToken cancellationToken)
    {
        // Validate chat history
        ArgumentNullException.ThrowIfNull(history);
        if (history.Count == 0)
        {
            throw new ArgumentException("The message list cannot be empty.", nameof(history));
        }

        if (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException("The operation was canceled.", cancellationToken);
        }

        // Throw if cancellation is requested after validation
        cancellationToken.ThrowIfCancellationRequested();
    }
}
