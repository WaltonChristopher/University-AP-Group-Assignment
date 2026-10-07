using Yodaphone.Web.Domain;

namespace Yodaphone.Web.Services;

/// <summary>
/// Stores and retrieves a conversation together with its messages for the owning user.
/// </summary>
/// <remarks>
/// Keeps storage details out of <see cref="ChatService"/>. Conversation IDs are
/// domain GUIDs. Implementations handle their mapping to database keys.
/// Every operation is scoped to the supplied owner ID.
/// </remarks>
public interface IConversationRepository
{
    /// <summary>
    /// Persists a new conversation and any messages it already contains.
    /// </summary>
    /// <param name="conversation">The new conversation to store.</param>
    /// <param name="userId">The positive, server-trusted ID of the conversation owner.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>A task that completes after the conversation has been saved.</returns>
    Task AddAsync(Conversation conversation, int userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads a conversation and its ordered messages for the specified owner.
    /// </summary>
    /// <param name="conversationId">The stable domain GUID, rather than an integer database key.</param>
    /// <param name="userId">The positive, server-trusted ID of the conversation owner.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>The restored conversation, or <see langword="null"/> if no matching owned conversation exists.</returns>
    Task<Conversation?> GetAsync(Guid conversationId, int userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the owner's open conversation with the most recent activity timestamp.
    /// </summary>
    /// <param name="userId">The positive, server-trusted ID of the conversation owner.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>The restored conversation, or <see langword="null"/> if the owner has no open conversations.</returns>
    Task<Conversation?> GetLatestActiveAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing conversation's metadata and appends messages not yet stored.
    /// </summary>
    /// <remarks>
    /// Messages are immutable: existing message rows are neither edited nor deleted.
    /// Saving the same message GUID again must not create a duplicate row.
    /// </remarks>
    /// <param name="conversation">The existing conversation containing the changes to save.</param>
    /// <param name="userId">The positive, server-trusted ID of the conversation owner.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>A task that completes after the changes have been saved.</returns>
    /// <exception cref="KeyNotFoundException">No conversation with this GUID belongs to the specified owner.</exception>
    Task SaveAsync(Conversation conversation, int userId, CancellationToken cancellationToken = default);
}
