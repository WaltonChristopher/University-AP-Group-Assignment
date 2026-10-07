using Yodaphone.Web.Domain;

namespace Yodaphone.Web.Services;

/// <summary>
/// Coordinates domain conversations and agent replies for the UI.
/// Conversation IDs are string representations of domain GUIDs.
/// </summary>
public interface IChatService
{
    Task<Conversation> StartConversationAsync(
        CancellationToken cancellationToken = default
    );

    Task<Conversation> GetConversationAsync(
        string conversationId,
        CancellationToken cancellationToken = default
    );

    Task<ChatMessage> AddMessageAsync(
        string conversationId,
        MessageRole role,
        string content,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Adds a customer message and returns the assistant reply. If the agent
    /// fails, the customer message remains in the conversation.
    /// </summary>
    Task<ChatMessage> SendMessageAsync(
        string conversationId,
        string content,
        CancellationToken cancellationToken = default
    );

    Task CloseConversationAsync(
        string conversationId,
        CancellationToken cancellationToken = default
    );
}
