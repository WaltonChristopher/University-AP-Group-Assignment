using Microsoft.EntityFrameworkCore;
using Yodaphone.Web.Data;
using DataConversation = Yodaphone.Web.Data.Entities.Conversation;
using DataMessage = Yodaphone.Web.Data.Entities.Message;
using DataUser = Yodaphone.Web.Data.Entities.User;
using DomainConversation = Yodaphone.Web.Domain.Conversation;
using DomainMessage = Yodaphone.Web.Domain.ChatMessage;
using Yodaphone.Web.Domain;

namespace Yodaphone.Web.Services;

/// <summary>
/// Saves domain conversations and their messages in SQLite through Microsoft EF Core entities.
/// </summary>
/// <remarks>
/// Creates and disposes a context for each operation, so neither EF tracking state
/// nor a database context survives for the lifetime of a Blazor circuit.
/// Returned domain objects can be used after their read context is disposed.
/// </remarks>
public sealed class SqliteConversationRepository : IConversationRepository
{
    private readonly IDbContextFactory<ApplicationDbContext> dbContextFactory;

    /// <summary>
    /// Creates a repository whose operations obtain their own database contexts.
    /// </summary>
    /// <param name="dbContextFactory">The configured EF Core factory for accessing the SQLite database.</param>
    public SqliteConversationRepository(IDbContextFactory<ApplicationDbContext> dbContextFactory)
    {
        ArgumentNullException.ThrowIfNull(dbContextFactory);
        this.dbContextFactory = dbContextFactory;
    }

    /// <inheritdoc />
    public async Task AddAsync(DomainConversation conversation, int userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(conversation);
        ValidateUserId(userId);

        // A Blazor circuit can outlive many operations. Keep tracking and the
        // context lifetime inside this database operation, including on failure.
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await EnsureUserExistsAsync(dbContext, userId, cancellationToken);

        var entity = new DataConversation
        {
            Id = conversation.Id,
            UserId = userId
        };
        CopyConversation(conversation, entity);
        foreach (var message in conversation.Messages)
        {
            // Linking through the navigation collection lets EF populate each
            // message's integer ConversationId foreign key when it saves.
            entity.Messages.Add(ToEntity(message, userId));
        }

        dbContext.Conversations.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<DomainConversation?> GetAsync(Guid conversationId, int userId, CancellationToken cancellationToken = default)
    {
        ValidateUserId(userId);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        // Read-only entities need no change tracking. Load messages now because
        // the context will be disposed before the domain conversation is returned.
        var entity = await dbContext.Conversations
            .AsNoTracking()
            .Include(conversation => conversation.Messages)
            .SingleOrDefaultAsync(
                conversation => conversation.Id == conversationId && conversation.UserId == userId,
                cancellationToken);

        return entity is null ? null : ToDomain(entity);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DomainConversation>> GetAllAsync(int userId, CancellationToken cancellationToken = default)
    {
        ValidateUserId(userId);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var entities = await dbContext.Conversations
            .AsNoTracking()
            .Include(conversation => conversation.Messages)
            .Where(conversation => conversation.UserId == userId)
            .OrderByDescending(conversation => conversation.LastUpdated)
            // Database keys give a stable order when activity timestamps match.
            .ThenByDescending(conversation => conversation.ConversationId)
            .ToListAsync(cancellationToken);

        return entities.Select(ToDomain).ToArray();
    }

    /// <inheritdoc />
    public async Task<DomainConversation?> GetLatestActiveAsync(int userId, CancellationToken cancellationToken = default)
    {
        ValidateUserId(userId);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var entity = await dbContext.Conversations
            .AsNoTracking()
            .Include(conversation => conversation.Messages)
            .Where(conversation => conversation.UserId == userId && !conversation.Closed)
            .OrderByDescending(conversation => conversation.LastUpdated)
            .FirstOrDefaultAsync(cancellationToken);

        return entity is null ? null : ToDomain(entity);
    }

    /// <inheritdoc />
    public async Task SaveAsync(DomainConversation conversation, int userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(conversation);
        ValidateUserId(userId);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var entity = await dbContext.Conversations
            .Include(storedConversation => storedConversation.Messages)
            .SingleOrDefaultAsync(
                storedConversation => storedConversation.Id == conversation.Id && storedConversation.UserId == userId,
                cancellationToken)
            ?? throw new KeyNotFoundException($"Conversation '{conversation.Id}' was not found.");

        CopyConversation(conversation, entity);

        // Each save uses a fresh context, so compare persisted GUIDs rather than
        // object references to avoid inserting earlier messages a second time.
        var storedMessageIds = entity.Messages.Select(message => message.Id).ToHashSet();
        foreach (var message in conversation.Messages.Where(message => !storedMessageIds.Contains(message.Id)))
        {
            entity.Messages.Add(ToEntity(message, userId));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Adds the configured development user if needed. The caller saves that user
    /// together with the new conversation in the same database operation.
    /// </summary>
    private static async Task EnsureUserExistsAsync(ApplicationDbContext dbContext, int userId, CancellationToken cancellationToken)
    {
        if (await dbContext.Users.AnyAsync(user => user.UserId == userId, cancellationToken))
        {
            return;
        }

        dbContext.Users.Add(new DataUser { UserId = userId, Support = false });
    }

    /// <summary>
    /// Preserves a message's domain GUID, content, timestamp, and role in a new database row.
    /// </summary>
    /// <remarks>
    /// EF generates the integer message key and fills the conversation foreign key
    /// when this row is added to the parent conversation's message collection.
    /// </remarks>
    private static DataMessage ToEntity(DomainMessage message, int userId) => new()
    {
        Id = message.Id,
        MessageContent = message.Content,
        DateTime = message.SentAt.UtcDateTime,
        // The existing schema stores the role name in Status, not a delivery status.
        Status = message.Role.ToString(),
        // The database's sender FK points at the conversation owner. Message role
        // distinguishes customer, assistant, and system messages.
        SenderId = userId
    };

    /// <summary>
    /// Copies conversation metadata, storing timestamps in UTC and status as a closed flag.
    /// </summary>
    private static void CopyConversation(DomainConversation source, DataConversation destination)
    {
        // Both existing schema fields represent the domain's start time.
        destination.Start = source.StartedAt.UtcDateTime;
        destination.DateTime = source.StartedAt.UtcDateTime;
        destination.LastUpdated = source.LastActivityAt.UtcDateTime;
        destination.Closed = source.Status == ConversationStatus.Closed;
    }

    /// <summary>
    /// Restores a domain conversation without generating new IDs or activity timestamps.
    /// </summary>
    private static DomainConversation ToDomain(DataConversation entity)
    {
        // Integer message keys break timestamp ties in insertion order. Random
        // domain GUIDs would not preserve the original conversation sequence.
        var messages = entity.Messages
            .OrderBy(message => message.DateTime)
            .ThenBy(message => message.MessageId)
            .Select(message => DomainMessage.Restore(
                message.Id,
                entity.Id,
                ParseRole(message.Status),
                message.MessageContent,
                // SQLite returns DateTime values without a timezone marker; they
                // were written in UTC and must not be interpreted as local time.
                new DateTimeOffset(DateTime.SpecifyKind(message.DateTime, DateTimeKind.Utc))))
            .ToArray();

        return DomainConversation.Restore(
            entity.Id,
            new DateTimeOffset(DateTime.SpecifyKind(entity.Start, DateTimeKind.Utc)),
            new DateTimeOffset(DateTime.SpecifyKind(entity.LastUpdated, DateTimeKind.Utc)),
            entity.Closed ? ConversationStatus.Closed : ConversationStatus.Active,
            messages);
    }

    /// <summary>
    /// Reads a stored role and rejects corrupt values, including undefined numeric enum values.
    /// </summary>
    private static MessageRole ParseRole(string value) =>
        Enum.TryParse<MessageRole>(value, ignoreCase: false, out var role) && Enum.IsDefined(role)
            ? role
            : throw new InvalidOperationException($"Stored message role '{value}' is invalid.");

    private static void ValidateUserId(int userId)
    {
        if (userId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(userId), "User ID must be a positive integer.");
        }
    }
}
