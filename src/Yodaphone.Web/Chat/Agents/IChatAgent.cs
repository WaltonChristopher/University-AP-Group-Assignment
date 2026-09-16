using Yodaphone.Web.Domain;

namespace Yodaphone.Web.Chat.Agents;

/// <summary>
/// Defines the interface for a chat agent that can provide responses in a conversation.
/// </summary>
public interface IChatAgent
{
    /// <summary>
    /// Asynchronously gets a reply based on a non-empty chat history.
    /// </summary>
    /// <param name="history">The chat history to respond to.</param>
    /// <param name="cancellationToken">A token used to cancel the asynchronous operation.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="history"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="history"/> is empty.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Thrown when <paramref name="cancellationToken"/> has been cancelled.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the response is empty or null.
    /// </exception>
    Task<AgentChatResponse> GetReplyAsync(
        IReadOnlyList<ChatMessage> history,
        CancellationToken cancellationToken
    );
}
