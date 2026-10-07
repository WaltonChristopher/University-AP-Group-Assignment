using Yodaphone.Web.Domain;
using Yodaphone.Web.Chat.Agents;

namespace Yodaphone.Web.Services;

/// <summary>
/// Coordinates conversation changes and AI replies. Conversations are held
/// only for this service instance until a persistence layer is available.
/// </summary>
public class ChatService : IChatService
{
    private readonly IChatAgent chatAgent;

    // TODO: Replace this temporary collection with IConversationRepository.
    // This is not durable storage. Conversations disappear with this instance.
    private readonly Dictionary<Guid, Conversation> conversations = [];

    // Serialize operations so a close or another send cannot change history
    // while an agent response is being generated.
    private readonly SemaphoreSlim operationLock = new(1, 1);

    public ChatService(IChatAgent chatAgent)
    {
        ArgumentNullException.ThrowIfNull(chatAgent);
        this.chatAgent = chatAgent;
    }

    public async Task<Conversation> StartConversationAsync(CancellationToken cancellationToken = default)
    {
        await operationLock.WaitAsync(cancellationToken);
        try
        {
            var conversation = new Conversation(Guid.NewGuid(), DateTimeOffset.UtcNow);
            conversations.Add(conversation.Id, conversation);
            // TODO: Add and save the conversation through the repository.
            return conversation;
        }
        finally
        {
            operationLock.Release();
        }
    }

    public async Task<Conversation> GetConversationAsync(string conversationId, CancellationToken cancellationToken = default)
    {
        await operationLock.WaitAsync(cancellationToken);
        try
        {
            return GetConversation(conversationId);
        }
        finally
        {
            operationLock.Release();
        }
    }

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
            var message = GetConversation(conversationId).AddMessage(role, content.Trim());
            // TODO: Save changes through the repository.
            return message;
        }
        finally
        {
            operationLock.Release();
        }
    }

    public async Task<ChatMessage> SendMessageAsync(string conversationId, string content, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);
        await operationLock.WaitAsync(cancellationToken);
        try
        {
            var conversation = GetConversation(conversationId);
            conversation.AddMessage(MessageRole.Customer, content.Trim());
            // TODO: Save the customer message before requesting the reply.

            var response = await chatAgent.GetReplyAsync(
                conversation.Messages.ToList(), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            if (response is null || string.IsNullOrWhiteSpace(response.Content))
            {
                throw new InvalidOperationException("The chat agent returned an empty reply.");
            }

            var reply = conversation.AddMessage(MessageRole.Assistant, response.Content);
            // TODO: Save the assistant message through the repository.
            return reply;
        }
        finally
        {
            operationLock.Release();
        }
    }

    public async Task CloseConversationAsync(string conversationId, CancellationToken cancellationToken = default)
    {
        await operationLock.WaitAsync(cancellationToken);
        try
        {
            GetConversation(conversationId).Close();
            // TODO: Save the closed status through the repository.
        }
        finally
        {
            operationLock.Release();
        }
    }

    private Conversation GetConversation(string conversationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);
        if (!Guid.TryParse(conversationId, out var id))
        {
            throw new ArgumentException("Conversation ID must be a valid GUID.", nameof(conversationId));
        }

        // TODO: Load the conversation and its messages through the repository.
        return conversations.TryGetValue(id, out var conversation)
            ? conversation
            : throw new KeyNotFoundException($"Conversation '{id}' was not found.");
    }
}
