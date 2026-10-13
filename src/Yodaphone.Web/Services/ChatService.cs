using Yodaphone.Web.Domain;
using Yodaphone.Web.Chat.Agents;

namespace Yodaphone.Web.Services;

/// <summary>
/// Coordinates persisted conversation changes and AI replies.
/// </summary>
/// <remarks>
/// The domain enforces conversation rules, the repository owns storage, and the
/// agent generates replies. User IDs come from the server-side current-user service.
/// </remarks>
public class ChatService : IChatService
{
    private readonly IChatAgent chatAgent;
    private readonly IConversationRepository conversationRepository;
    private readonly ICurrentUser currentUser;
    private readonly ILogger<ChatService> logger;

    // Serialize operations within this service instance so a close or another
    // send cannot change history while its agent response is being generated.
    // This does not lock other Blazor circuits or other service instances.
    private readonly SemaphoreSlim operationLock = new(1, 1);

    /// <summary>
    /// Creates a service using the selected AI provider, storage implementation, and user identity.
    /// </summary>
    /// <param name="chatAgent">Generates assistant replies from the ordered message history.</param>
    /// <param name="conversationRepository">Saves and restores conversations for their owner.</param>
    /// <param name="currentUser">Supplies the server-trusted owner ID for every operation.</param>
    /// <param name="logger">Records operation failures and their diagnostic context.</param>
    public ChatService(
        IChatAgent chatAgent,
        IConversationRepository conversationRepository,
        ICurrentUser currentUser,
        ILogger<ChatService> logger)
    {
        ArgumentNullException.ThrowIfNull(chatAgent);
        ArgumentNullException.ThrowIfNull(conversationRepository);
        ArgumentNullException.ThrowIfNull(currentUser);
        ArgumentNullException.ThrowIfNull(logger);
        this.chatAgent = chatAgent;
        this.conversationRepository = conversationRepository;
        this.currentUser = currentUser;
        this.logger = logger;
    }

    /// <inheritdoc />
    public Task<Conversation> StartConversationAsync(CancellationToken cancellationToken = default)
    {
        var conversationId = Guid.NewGuid();
        return RunOperationAsync(nameof(StartConversationAsync), conversationId.ToString(), async () =>
        {
            var conversation = new Conversation(conversationId, DateTimeOffset.UtcNow);
            await conversationRepository.AddAsync(conversation, currentUser.UserId, cancellationToken);
            return conversation;
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task<Conversation> GetConversationAsync(string conversationId, CancellationToken cancellationToken = default) =>
        RunOperationAsync(nameof(GetConversationAsync), conversationId,
            () => LoadConversationAsync(conversationId, cancellationToken), cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<Conversation>> GetConversationsAsync(CancellationToken cancellationToken = default) =>
        RunOperationAsync(nameof(GetConversationsAsync), null,
            () => conversationRepository.GetAllAsync(currentUser.UserId, cancellationToken), cancellationToken);

    /// <inheritdoc />
    public Task<Conversation?> GetActiveConversationAsync(CancellationToken cancellationToken = default) =>
        RunOperationAsync(nameof(GetActiveConversationAsync), null,
            () => conversationRepository.GetLatestActiveAsync(currentUser.UserId, cancellationToken), cancellationToken);

    /// <inheritdoc />
    public Task<ChatMessage> AddMessageAsync(string conversationId, MessageRole role, string content, CancellationToken cancellationToken = default) =>
        RunOperationAsync(nameof(AddMessageAsync), conversationId, async () =>
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(content);
            if (!Enum.IsDefined(role))
            {
                throw new ArgumentOutOfRangeException(nameof(role));
            }

            var conversation = await LoadConversationAsync(conversationId, cancellationToken);
            var message = conversation.AddMessage(role, content.Trim());
            await conversationRepository.SaveAsync(conversation, currentUser.UserId, cancellationToken);
            return message;
        }, cancellationToken);

    /// <inheritdoc />
    public Task<ChatMessage> SendMessageAsync(string conversationId, string content, CancellationToken cancellationToken = default) =>
        RunOperationAsync(nameof(SendMessageAsync), conversationId, async () =>
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(content);
            var conversation = await LoadConversationAsync(conversationId, cancellationToken);
            conversation.AddMessage(MessageRole.Customer, content.Trim());
            // Commit the message before requesting the reply. The repository disposes its
            // context here, so the network call holds no database context open and an agent 
            // failure or cancellation cannot undo this message.
            await conversationRepository.SaveAsync(conversation, currentUser.UserId, cancellationToken);

            var response = await chatAgent.GetReplyAsync(
                conversation.Messages.ToList(), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            if (response is null || string.IsNullOrWhiteSpace(response.Content))
            {
                throw new InvalidOperationException("The chat agent returned an empty reply.");
            }

            var reply = conversation.AddMessage(MessageRole.Assistant, response.Content);
            // Return success only after the assistant reply is durable as well.
            await conversationRepository.SaveAsync(conversation, currentUser.UserId, cancellationToken);
            return reply;
        }, cancellationToken);

    /// <inheritdoc />
    public Task CloseConversationAsync(string conversationId, CancellationToken cancellationToken = default) =>
        RunOperationAsync(nameof(CloseConversationAsync), conversationId, async () =>
        {
            var conversation = await LoadConversationAsync(conversationId, cancellationToken);
            conversation.Close();
            await conversationRepository.SaveAsync(conversation, currentUser.UserId, cancellationToken);
            return true;
        }, cancellationToken);

    // Log once at the service boundary so storage, domain, and agent failures
    // have the same context. The UI handles the error without logging it again.
    private async Task<T> RunOperationAsync<T>(
        string operationName, string? conversationId, Func<Task<T>> operation, CancellationToken cancellationToken)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["OperationId"] = Guid.NewGuid(),
            ["Operation"] = operationName,
            // Only record validated IDs, never arbitrary input supplied as an ID.
            ["ConversationId"] = Guid.TryParse(conversationId, out var id) ? id : null,
            ["ChatAgent"] = chatAgent.GetType().Name
        });

        var lockAcquired = false;
        try
        {
            await operationLock.WaitAsync(cancellationToken);
            lockAcquired = true;
            return await operation();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Closing a page or cancelling a request is an expected outcome.
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Chat operation {Operation} failed", operationName);
            throw;
        }
        finally
        {
            if (lockAcquired)
            {
                operationLock.Release();
            }
        }
    }

    /// <summary>
    /// Validates the UI's GUID string and loads only a conversation owned by the current user.
    /// </summary>
    private async Task<Conversation> LoadConversationAsync(string conversationId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);
        if (!Guid.TryParse(conversationId, out var id))
        {
            throw new ArgumentException("Conversation ID must be a valid GUID.", nameof(conversationId));
        }

        return await conversationRepository.GetAsync(id, currentUser.UserId, cancellationToken)
            ?? throw new KeyNotFoundException($"Conversation '{id}' was not found.");
    }
}
