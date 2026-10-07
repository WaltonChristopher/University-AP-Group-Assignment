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
    public ChatService(
        IChatAgent chatAgent,
        IConversationRepository conversationRepository,
        ICurrentUser currentUser)
    {
        ArgumentNullException.ThrowIfNull(chatAgent);
        ArgumentNullException.ThrowIfNull(conversationRepository);
        ArgumentNullException.ThrowIfNull(currentUser);
        this.chatAgent = chatAgent;
        this.conversationRepository = conversationRepository;
        this.currentUser = currentUser;
    }

    /// <inheritdoc />
    public async Task<Conversation> StartConversationAsync(CancellationToken cancellationToken = default)
    {
        await operationLock.WaitAsync(cancellationToken);
        try
        {
            var conversation = new Conversation(Guid.NewGuid(), DateTimeOffset.UtcNow);
            await conversationRepository.AddAsync(conversation, currentUser.UserId, cancellationToken);
            return conversation;
        }
        finally
        {
            operationLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<Conversation> GetConversationAsync(string conversationId, CancellationToken cancellationToken = default)
    {
        await operationLock.WaitAsync(cancellationToken);
        try
        {
            return await LoadConversationAsync(conversationId, cancellationToken);
        }
        finally
        {
            operationLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<Conversation?> GetActiveConversationAsync(CancellationToken cancellationToken = default)
    {
        await operationLock.WaitAsync(cancellationToken);
        try
        {
            return await conversationRepository.GetLatestActiveAsync(currentUser.UserId, cancellationToken);
        }
        finally
        {
            operationLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<ChatMessage> AddMessageAsync(string conversationId, MessageRole role, string content, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);
        if (!Enum.IsDefined(role))
        {
            throw new ArgumentOutOfRangeException(nameof(role));
        }

        await operationLock.WaitAsync(cancellationToken);
        try
        {
            var conversation = await LoadConversationAsync(conversationId, cancellationToken);
            var message = conversation.AddMessage(role, content.Trim());
            await conversationRepository.SaveAsync(conversation, currentUser.UserId, cancellationToken);
            return message;
        }
        finally
        {
            operationLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<ChatMessage> SendMessageAsync(string conversationId, string content, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);
        await operationLock.WaitAsync(cancellationToken);
        try
        {
            var conversation = await LoadConversationAsync(conversationId, cancellationToken);
            conversation.AddMessage(MessageRole.Customer, content.Trim());
            // Commit the messagebefore requesting the reply. The repository disposes its
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
        }
        finally
        {
            operationLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task CloseConversationAsync(string conversationId, CancellationToken cancellationToken = default)
    {
        await operationLock.WaitAsync(cancellationToken);
        try
        {
            var conversation = await LoadConversationAsync(conversationId, cancellationToken);
            conversation.Close();
            await conversationRepository.SaveAsync(conversation, currentUser.UserId, cancellationToken);
        }
        finally
        {
            operationLock.Release();
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
