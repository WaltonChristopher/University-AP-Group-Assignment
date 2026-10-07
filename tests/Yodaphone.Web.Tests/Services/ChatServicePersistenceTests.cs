using Xunit;
using Yodaphone.Web.Chat;
using Yodaphone.Web.Chat.Agents;
using Yodaphone.Web.Domain;
using Yodaphone.Web.Services;

namespace Yodaphone.Web.Tests.Services;

public sealed class ChatServicePersistenceTests
{
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

    private sealed class CallbackAgent(
        Func<IReadOnlyList<ChatMessage>, CancellationToken, Task<AgentChatResponse>> callback) : IChatAgent
    {
        public Task<AgentChatResponse> GetReplyAsync(IReadOnlyList<ChatMessage> history, CancellationToken cancellationToken) =>
            callback(history, cancellationToken);
    }

    private sealed class TestCurrentUser : ICurrentUser
    {
        public int UserId => 42;
    }
}
