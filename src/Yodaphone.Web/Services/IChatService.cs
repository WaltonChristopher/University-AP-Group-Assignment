using Yodaphone.Web.Domain;

namespace Yodaphone.Web.Services;

/// <summary>
/// Coordinates domain conversations and agent replies for the UI.
/// Conversation IDs are string representations of domain GUIDs.
/// </summary>
public interface IChatService
{
    /// <summary>
    /// Creates and persists an empty, active conversation for the current user.
    /// </summary>
    Task<Conversation> StartConversationAsync(
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Loads an existing conversation and its messages for the current user.
    /// </summary>
    /// <exception cref="ArgumentException">The conversation ID is not a valid GUID.</exception>
    /// <exception cref="KeyNotFoundException">The conversation does not exist or belongs to another user.</exception>
    Task<Conversation> GetConversationAsync(
        string conversationId,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Gets the current user's open conversation with the most recent activity, if one exists.
    /// </summary>
    Task<Conversation?> GetActiveConversationAsync(
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Appends and saves a message with the supplied role, without requesting an AI reply.
    /// </summary>
    /// <remarks>Used for messages such as the welcome greeting and help text.</remarks>
    Task<ChatMessage> AddMessageAsync(
        string conversationId,
        MessageRole role,
        string content,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Persists a customer message, requests an AI reply, then saves and returns the reply.
    /// </summary>
    /// <remarks>
    /// The customer message is committed before the AI call. Once saved, it remains
    /// available if the AI fails, the reply is empty, or the operation is cancelled.
    /// The database context is disposed before waiting for the AI; there is no
    /// database transaction spanning the customer message and the assistant reply.
    /// </remarks>
    Task<ChatMessage> SendMessageAsync(
        string conversationId,
        string content,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Persists the closed status, retaining the conversation and its messages for retrieval.
    /// </summary>
    /// <remarks>Closed conversations reject new messages.</remarks>
    Task CloseConversationAsync(
        string conversationId,
        CancellationToken cancellationToken = default
    );
}
