using Microsoft.EntityFrameworkCore;
using Xunit;
using Yodaphone.Web.Chat;
using Yodaphone.Web.Chat.Agents;
using Yodaphone.Web.Domain;
using Yodaphone.Web.Services;

namespace Yodaphone.Web.Tests.Services;

public sealed class ChatServicePersistenceTests
{
    [Fact]
    public async Task StartConversationAsync_creates_distinct_empty_chats_for_the_same_user()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var repository = new SqliteConversationRepository(database);
        var service = new ChatService(new OfflineChatAgent(), repository, new TestCurrentUser());
        var first = await service.StartConversationAsync();
        await service.AddMessageAsync(first.Id.ToString(), MessageRole.Customer, "First chat");

        // A new service instance must retain the same owner rather than create a new user.
        var reloadedService = new ChatService(new OfflineChatAgent(), repository, new TestCurrentUser());
        var second = await reloadedService.StartConversationAsync();

        Assert.NotEqual(Guid.Empty, second.Id);
        Assert.NotEqual(first.Id, second.Id);
        Assert.Empty(second.Messages);
        Assert.Equal(ConversationStatus.Active, second.Status);
        var restoredFirst = await reloadedService.GetConversationAsync(first.Id.ToString());
        Assert.Equal(ConversationStatus.Active, restoredFirst.Status);
        Assert.Equal("First chat", Assert.Single(restoredFirst.Messages).Content);
        var restoredSecond = await reloadedService.GetConversationAsync(second.Id.ToString());
        Assert.Empty(restoredSecond.Messages);

        await using (var context = database.CreateDbContext())
        {
            Assert.Equal(42, (await context.Users.SingleAsync()).UserId);
            var stored = await context.Conversations.ToListAsync();
            Assert.Equal(2, stored.Count);
            Assert.All(stored, conversation => Assert.Equal(42, conversation.UserId));
            Assert.Equal(2, stored.Select(conversation => conversation.ConversationId).Distinct().Count());
            Assert.Contains(stored, conversation => conversation.Id == first.Id);
            Assert.Contains(stored, conversation => conversation.Id == second.Id);
        }
        database.AssertAllContextsDisposed();
    }

    [Fact]
    public async Task SendMessageAsync_keeps_each_chats_agent_history_separate()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var histories = new List<ChatMessage[]>();
        var agent = new CallbackAgent((history, _) =>
        {
            histories.Add(history.ToArray());
            return Task.FromResult(new AgentChatResponse { Content = "Reply" });
        });
        var service = new ChatService(agent, new SqliteConversationRepository(database), new TestCurrentUser());
        var first = await service.StartConversationAsync();
        await service.SendMessageAsync(first.Id.ToString(), "First chat");
        var second = await service.StartConversationAsync();
        await service.SendMessageAsync(second.Id.ToString(), "Second chat");
        await service.SendMessageAsync(first.Id.ToString(), "Back to first");

        Assert.Equal(3, histories.Count);
        Assert.Equal("First chat", Assert.Single(histories[0]).Content);
        Assert.Equal("Second chat", Assert.Single(histories[1]).Content);
        Assert.Equal(new[] { "First chat", "Reply", "Back to first" }, histories[2].Select(message => message.Content));
        Assert.All(histories[2], message => Assert.Equal(first.Id, message.ConversationId));
        var restoredSecond = await service.GetConversationAsync(second.Id.ToString());
        Assert.Equal(new[] { "Second chat", "Reply" }, restoredSecond.Messages.Select(message => message.Content));
        database.AssertAllContextsDisposed();
    }

    [Fact]
    public async Task GetConversationsAsync_restores_active_and_closed_chats_only_for_the_current_user()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var repository = new SqliteConversationRepository(database);
        var service = new ChatService(new OfflineChatAgent(), repository, new TestCurrentUser());
        var first = await service.StartConversationAsync();
        await service.AddMessageAsync(first.Id.ToString(), MessageRole.Customer, "Saved first chat");
        await service.CloseConversationAsync(first.Id.ToString());
        var second = await service.StartConversationAsync();
        await service.AddMessageAsync(second.Id.ToString(), MessageRole.Customer, "Saved second chat");
        var otherService = new ChatService(new OfflineChatAgent(), repository, new TestCurrentUser(43));
        var otherChat = await otherService.StartConversationAsync();
        var reloadedService = new ChatService(new OfflineChatAgent(), repository, new TestCurrentUser());

        var chats = await reloadedService.GetConversationsAsync();

        Assert.Equal(new[] { second.Id, first.Id }, chats.Select(conversation => conversation.Id));
        Assert.Equal("Saved second chat", Assert.Single(chats[0].Messages).Content);
        Assert.Equal(ConversationStatus.Active, chats[0].Status);
        Assert.Equal("Saved first chat", Assert.Single(chats[1].Messages).Content);
        Assert.Equal(ConversationStatus.Closed, chats[1].Status);
        var restoredFirst = await reloadedService.GetConversationAsync(first.Id.ToString());
        Assert.Equal(chats[1].Messages.Single().Id, Assert.Single(restoredFirst.Messages).Id);
        Assert.Equal(otherChat.Id, Assert.Single(await otherService.GetConversationsAsync()).Id);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => reloadedService.GetConversationAsync(otherChat.Id.ToString()));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => otherService.GetConversationAsync(first.Id.ToString()));
        var newUserService = new ChatService(new OfflineChatAgent(), repository, new TestCurrentUser(44));
        Assert.Empty(await newUserService.GetConversationsAsync());
        database.AssertAllContextsDisposed();
    }

    [Fact]
    public async Task SendMessageAsync_commits_customer_before_agent_call_and_saves_reply_afterward()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        Guid conversationId = default;
        var agent = new CallbackAgent(async (history, cancellationToken) =>
        {
            // Read committed data through a fresh context while the AI call is in
            // progress, after every prior repository context has been disposed.
            database.AssertAllContextsDisposed();
            var saved = await new SqliteConversationRepository(database).GetAsync(conversationId, userId: 42, cancellationToken);
            Assert.NotNull(saved);
            var customer = Assert.Single(saved.Messages);
            Assert.Equal(MessageRole.Customer, customer.Role);
            Assert.Equal("Hello", customer.Content);
            Assert.Equal(customer.Id, Assert.Single(history).Id);
            database.AssertAllContextsDisposed();
            return new AgentChatResponse { Content = "How can I help?" };
        });
        var service = new ChatService(agent, new SqliteConversationRepository(database), new TestCurrentUser());
        conversationId = (await service.StartConversationAsync()).Id;

        var reply = await service.SendMessageAsync(conversationId.ToString(), " Hello ");

        var restored = await new SqliteConversationRepository(database).GetAsync(conversationId, userId: 42);
        Assert.NotNull(restored);
        Assert.Collection(restored.Messages,
            message => Assert.Equal(MessageRole.Customer, message.Role),
            message =>
            {
                Assert.Equal(reply.Id, message.Id);
                Assert.Equal(MessageRole.Assistant, message.Role);
                Assert.Equal("How can I help?", message.Content);
            });
        database.AssertAllContextsDisposed();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SendMessageAsync_preserves_customer_message_on_agent_failure_or_cancellation(bool cancel)
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        using var cancellation = new CancellationTokenSource();
        var agent = new CallbackAgent((_, cancellationToken) =>
        {
            database.AssertAllContextsDisposed();
            if (cancel)
            {
                cancellation.Cancel();
                cancellationToken.ThrowIfCancellationRequested();
            }
            throw new InvalidOperationException("Agent unavailable");
        });
        var service = new ChatService(agent, new SqliteConversationRepository(database), new TestCurrentUser());
        var conversation = await service.StartConversationAsync();

        if (cancel)
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                service.SendMessageAsync(conversation.Id.ToString(), "Hello", cancellation.Token));
        }
        else
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.SendMessageAsync(conversation.Id.ToString(), "Hello", cancellation.Token));
        }

        var restored = await new SqliteConversationRepository(database).GetAsync(conversation.Id, userId: 42);
        Assert.NotNull(restored);
        var message = Assert.Single(restored.Messages);
        Assert.Equal(MessageRole.Customer, message.Role);
        Assert.Equal("Hello", message.Content);
        database.AssertAllContextsDisposed();
    }

    [Fact]
    public async Task SendMessageAsync_rejects_reply_when_another_service_closes_the_chat_during_the_agent_call()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var agentStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseReply = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var agent = new CallbackAgent(async (_, cancellationToken) =>
        {
            database.AssertAllContextsDisposed();
            agentStarted.SetResult();
            await releaseReply.Task.WaitAsync(cancellationToken);
            return new AgentChatResponse { Content = "Late reply" };
        });
        var sendingService = new ChatService(agent, new SqliteConversationRepository(database), new TestCurrentUser());
        var closingService = new ChatService(new OfflineChatAgent(), new SqliteConversationRepository(database), new TestCurrentUser());
        var conversation = await sendingService.StartConversationAsync();
        var sending = sendingService.SendMessageAsync(conversation.Id.ToString(), "Hello");

        try
        {
            await agentStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await closingService.CloseConversationAsync(conversation.Id.ToString());
        }
        finally
        {
            releaseReply.TrySetResult();
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() => sending);
        var restored = await closingService.GetConversationAsync(conversation.Id.ToString());
        Assert.Equal(ConversationStatus.Closed, restored.Status);
        Assert.Equal("Hello", Assert.Single(restored.Messages).Content);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sendingService.SendMessageAsync(conversation.Id.ToString(), "Another message"));
        database.AssertAllContextsDisposed();
    }

    private sealed class CallbackAgent(
        Func<IReadOnlyList<ChatMessage>, CancellationToken, Task<AgentChatResponse>> callback) : IChatAgent
    {
        public Task<AgentChatResponse> GetReplyAsync(IReadOnlyList<ChatMessage> history, CancellationToken cancellationToken) =>
            callback(history, cancellationToken);
    }

    private sealed class TestCurrentUser(int userId = 42) : ICurrentUser
    {
        public int UserId => userId;
    }
}
