using Microsoft.EntityFrameworkCore;
using Xunit;
using Yodaphone.Web.Domain;
using Yodaphone.Web.Services;

namespace Yodaphone.Web.Tests.Services;

public sealed class SqliteConversationRepositoryTests
{
    [Fact]
    public async Task AddAsync_then_GetAsync_restores_identifiers_roles_order_status_and_utc_timestamps()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var repository = new SqliteConversationRepository(database);
        var startedAt = new DateTimeOffset(2026, 10, 7, 10, 0, 0, TimeSpan.FromHours(2));
        var conversationId = Guid.NewGuid();
        // Equal timestamps must retain insertion order rather than ordering by random GUIDs.
        var messages = new[]
        {
            ChatMessage.Restore(Guid.NewGuid(), conversationId, MessageRole.System, "Instructions", startedAt.AddMinutes(1)),
            ChatMessage.Restore(Guid.NewGuid(), conversationId, MessageRole.Customer, "I need help", startedAt.AddMinutes(1)),
            ChatMessage.Restore(Guid.NewGuid(), conversationId, MessageRole.Assistant, "Welcome", startedAt.AddMinutes(1))
        };
        var conversation = Conversation.Restore(
            conversationId, startedAt, startedAt.AddMinutes(1), ConversationStatus.Closed, messages);

        await repository.AddAsync(conversation, userId: 42);
        database.AssertAllContextsDisposed();
        var restored = await new SqliteConversationRepository(database).GetAsync(conversation.Id, userId: 42);

        Assert.NotNull(restored);
        Assert.Equal(conversation.Id, restored.Id);
        Assert.Equal(ConversationStatus.Closed, restored.Status);
        Assert.Equal(conversation.StartedAt, restored.StartedAt);
        Assert.Equal(conversation.LastActivityAt, restored.LastActivityAt);
        Assert.Equal(TimeSpan.Zero, restored.StartedAt.Offset);
        Assert.Equal(TimeSpan.Zero, restored.LastActivityAt.Offset);
        Assert.Equal(messages.Length, restored.Messages.Count);
        foreach (var (expected, actual) in messages.Zip(restored.Messages))
        {
            Assert.Equal(expected.Id, actual.Id);
            Assert.Equal(expected.ConversationId, actual.ConversationId);
            Assert.Equal(expected.Role, actual.Role);
            Assert.Equal(expected.Content, actual.Content);
            Assert.Equal(expected.SentAt, actual.SentAt);
            Assert.Equal(TimeSpan.Zero, actual.SentAt.Offset);
        }
        database.AssertAllContextsDisposed();
    }

    [Fact]
    public async Task SaveAsync_appends_messages_without_duplicates_and_persists_close()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var repository = new SqliteConversationRepository(database);
        var conversation = new Conversation(Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(-5));
        await repository.AddAsync(conversation, userId: 7);

        var customerMessage = conversation.AddMessage(MessageRole.Customer, "Hello");
        await repository.SaveAsync(conversation, userId: 7);
        var reply = conversation.AddMessage(MessageRole.Assistant, "How can I help?");
        conversation.Close();
        await repository.SaveAsync(conversation, userId: 7);
        await repository.SaveAsync(conversation, userId: 7);

        var restored = await new SqliteConversationRepository(database).GetAsync(conversation.Id, userId: 7);
        Assert.NotNull(restored);
        Assert.Equal(ConversationStatus.Closed, restored.Status);
        Assert.Equal(conversation.LastActivityAt, restored.LastActivityAt);
        Assert.Equal(new[] { customerMessage.Id, reply.Id }, restored.Messages.Select(message => message.Id));
        database.AssertAllContextsDisposed();
    }

    [Fact]
    public async Task GetAsync_and_SaveAsync_do_not_access_another_users_conversation()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var repository = new SqliteConversationRepository(database);
        var conversation = new Conversation(Guid.NewGuid(), DateTimeOffset.UtcNow);
        await repository.AddAsync(conversation, userId: 7);

        Assert.Null(await repository.GetAsync(conversation.Id, userId: 8));
        conversation.Close();
        await Assert.ThrowsAsync<KeyNotFoundException>(() => repository.SaveAsync(conversation, userId: 8));
        var restored = await repository.GetAsync(conversation.Id, userId: 7);
        Assert.NotNull(restored);
        Assert.Equal(ConversationStatus.Active, restored.Status);
        database.AssertAllContextsDisposed();
    }

    [Fact]
    public async Task GetLatestActiveAsync_returns_the_latest_open_conversation_for_the_user()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var repository = new SqliteConversationRepository(database);
        var older = new Conversation(Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(-10));
        var newer = new Conversation(Guid.NewGuid(), DateTimeOffset.UtcNow);
        newer.AddMessage(MessageRole.Customer, "Still open");
        var closed = new Conversation(Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(1));
        closed.Close();
        var otherUsersConversation = new Conversation(Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(2));
        await repository.AddAsync(older, userId: 9);
        await repository.AddAsync(newer, userId: 9);
        await repository.AddAsync(closed, userId: 9);
        await repository.AddAsync(otherUsersConversation, userId: 10);

        var restored = await repository.GetLatestActiveAsync(userId: 9);
        Assert.NotNull(restored);
        Assert.Equal(newer.Id, restored.Id);
        database.AssertAllContextsDisposed();
    }

    [Theory]
    [InlineData("Unknown")]
    [InlineData("999")]
    public async Task GetAsync_rejects_invalid_stored_roles_and_disposes_context(string invalidRole)
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var repository = new SqliteConversationRepository(database);
        var conversation = new Conversation(Guid.NewGuid(), DateTimeOffset.UtcNow);
        conversation.AddMessage(MessageRole.Customer, "Hello");
        await repository.AddAsync(conversation, userId: 7);
        await using (var context = database.CreateDbContext())
        {
            var message = await context.Messages.SingleAsync();
            message.Status = invalidRole;
            await context.SaveChangesAsync();
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.GetAsync(conversation.Id, userId: 7));
        database.AssertAllContextsDisposed();
    }
}
