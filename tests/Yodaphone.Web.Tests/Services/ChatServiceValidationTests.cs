using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Yodaphone.Web.Chat;
using Yodaphone.Web.Chat.Agents;
using Yodaphone.Web.Domain;
using Yodaphone.Web.Services;

namespace Yodaphone.Web.Tests.Services;

public sealed class ChatServiceValidationTests
{
    [Theory]
    [InlineData("agent")]
    [InlineData("repository")]
    [InlineData("user")]
    [InlineData("logger")]
    public void Constructor_RejectsMissingDependencies(string missing)
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new ChatService(
            missing == "agent" ? null! : new OfflineChatAgent(),
            missing == "repository" ? null! : new UnusedRepository(),
            missing == "user" ? null! : new CurrentUser(),
            missing == "logger" ? null! : NullLogger<ChatService>.Instance));
        Assert.Equal(missing switch
        {
            "agent" => "chatAgent",
            "repository" => "conversationRepository",
            "user" => "currentUser",
            _ => "logger"
        }, exception.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("invalid-guid")]
    public async Task GetConversationAsync_RejectsInvalidIdAndReleasesLock(string? id)
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var service = CreateService(database);
        await Assert.ThrowsAnyAsync<ArgumentException>(() => service.GetConversationAsync(id!));
        var conversation = await service.StartConversationAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotEqual(Guid.Empty, conversation.Id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \n")]
    public async Task AddAndSendMessageAsync_RejectBlankContentWithoutSaving(string? content)
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var service = CreateService(database);
        var conversation = await service.StartConversationAsync();
        await Assert.ThrowsAnyAsync<ArgumentException>(() => service.AddMessageAsync(conversation.Id.ToString(), MessageRole.Assistant, content!));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => service.SendMessageAsync(conversation.Id.ToString(), content!));
        Assert.Empty((await service.GetConversationAsync(conversation.Id.ToString())).Messages);
    }

    [Fact]
    public async Task AddMessageAsync_RejectsUnknownRoleWithoutSaving()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var service = CreateService(database);
        var conversation = await service.StartConversationAsync();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.AddMessageAsync(conversation.Id.ToString(), (MessageRole)999, "Hello"));
        Assert.Empty((await service.GetConversationAsync(conversation.Id.ToString())).Messages);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t")]
    public async Task SendMessageAsync_RejectsEmptyReplyButRetainsCustomerAndReleasesLock(string? reply)
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var agent = new CallbackAgent(_ => Task.FromResult(reply is null ? null! : new AgentChatResponse { Content = reply }));
        var service = CreateService(database, agent);
        var conversation = await service.StartConversationAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SendMessageAsync(conversation.Id.ToString(), "Question"));
        var restored = await service.GetConversationAsync(conversation.Id.ToString()).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("Question", Assert.Single(restored.Messages).Content);
        Assert.Equal(MessageRole.Customer, restored.Messages.Single().Role);
    }

    [Fact]
    public async Task SendMessageAsync_CancellationAfterAgentReplyDoesNotSaveAssistant()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        using var cancellation = new CancellationTokenSource();
        var agent = new CallbackAgent(_ =>
        {
            cancellation.Cancel();
            return Task.FromResult(new AgentChatResponse { Content = "Late reply" });
        });
        var service = CreateService(database, agent);
        var conversation = await service.StartConversationAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.SendMessageAsync(conversation.Id.ToString(), "Question", cancellation.Token));
        var restored = await service.GetConversationAsync(conversation.Id.ToString());
        Assert.Equal(MessageRole.Customer, Assert.Single(restored.Messages).Role);
    }

    private static ChatService CreateService(SqliteTestDatabase database, IChatAgent? agent = null) =>
        new(agent ?? new OfflineChatAgent(), new SqliteConversationRepository(database), new CurrentUser(), NullLogger<ChatService>.Instance);

    private sealed class CurrentUser : ICurrentUser { public int UserId => 42; }
    private sealed class CallbackAgent(Func<CancellationToken, Task<AgentChatResponse>> reply) : IChatAgent
    {
        public Task<AgentChatResponse> GetReplyAsync(IReadOnlyList<ChatMessage> history, CancellationToken cancellationToken) => reply(cancellationToken);
    }
    private sealed class UnusedRepository : IConversationRepository
    {
        public Task AddAsync(Conversation conversation, int userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Conversation?> GetAsync(Guid id, int userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Conversation>> GetAllAsync(int userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Conversation?> GetLatestActiveAsync(int userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SaveAsync(Conversation conversation, int userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
