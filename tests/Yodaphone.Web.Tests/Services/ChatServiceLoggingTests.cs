using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Extensions.Logging;
using Xunit;
using Yodaphone.Web.Chat;
using Yodaphone.Web.Chat.Agents;
using Yodaphone.Web.Domain;
using Yodaphone.Web.Services;

namespace Yodaphone.Web.Tests.Services;

public sealed class ChatServiceLoggingTests
{
    [Theory]
    [InlineData(nameof(ChatService.StartConversationAsync))]
    [InlineData(nameof(ChatService.GetConversationAsync))]
    [InlineData(nameof(ChatService.GetConversationsAsync))]
    [InlineData(nameof(ChatService.GetActiveConversationAsync))]
    [InlineData(nameof(ChatService.AddMessageAsync))]
    [InlineData(nameof(ChatService.SendMessageAsync))]
    [InlineData(nameof(ChatService.CloseConversationAsync))]
    public async Task Repository_failures_are_logged_once_with_operation_context_and_rethrown(string operationName)
    {
        using var logs = new CapturedLogs();
        var failure = new InvalidOperationException("Storage unavailable");
        var conversationId = Guid.NewGuid();
        var service = new ChatService(new OfflineChatAgent(), new FailingRepository(failure), new TestCurrentUser(), logs.Logger);
        Func<Task> operation = operationName switch
        {
            nameof(ChatService.StartConversationAsync) => () => service.StartConversationAsync(),
            nameof(ChatService.GetConversationAsync) => () => service.GetConversationAsync(conversationId.ToString()),
            nameof(ChatService.GetConversationsAsync) => () => service.GetConversationsAsync(),
            nameof(ChatService.GetActiveConversationAsync) => () => service.GetActiveConversationAsync(),
            nameof(ChatService.AddMessageAsync) => () => service.AddMessageAsync(conversationId.ToString(), MessageRole.Assistant, "Private greeting"),
            nameof(ChatService.SendMessageAsync) => () => service.SendMessageAsync(conversationId.ToString(), "Private customer message"),
            nameof(ChatService.CloseConversationAsync) => () => service.CloseConversationAsync(conversationId.ToString()),
            _ => throw new ArgumentOutOfRangeException(nameof(operationName))
        };

        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(operation));

        var entry = Assert.Single(logs.Events);
        Assert.Equal(LogEventLevel.Error, entry.Level);
        Assert.Same(failure, entry.Exception);
        Assert.Equal(operationName, Assert.IsType<ScalarValue>(entry.Properties["Operation"]).Value);
        Assert.IsType<Guid>(Assert.IsType<ScalarValue>(entry.Properties["OperationId"]).Value);
        Assert.Equal(nameof(OfflineChatAgent), Assert.IsType<ScalarValue>(entry.Properties["ChatAgent"]).Value);
        if (operationName is not nameof(ChatService.StartConversationAsync)
            and not nameof(ChatService.GetConversationsAsync) and not nameof(ChatService.GetActiveConversationAsync))
        {
            Assert.Equal(conversationId, Assert.IsType<ScalarValue>(entry.Properties["ConversationId"]).Value);
        }
        Assert.DoesNotContain("Private", entry.RenderMessage() + string.Join(" ", entry.Properties.Values));
    }

    [Fact]
    public async Task Agent_failure_keeps_inner_exception_and_saved_message_out_of_log_properties()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        using var logs = new CapturedLogs();
        var failure = new ChatAgentException(new InvalidOperationException("Provider unavailable"));
        var service = new ChatService(new CallbackAgent((_, _) => throw failure),
            new SqliteConversationRepository(database), new TestCurrentUser(), logs.Logger);
        var conversation = await service.StartConversationAsync();

        Assert.Same(failure, await Assert.ThrowsAsync<ChatAgentException>(() =>
            service.SendMessageAsync(conversation.Id.ToString(), "Private customer message")));

        var restored = await service.GetConversationAsync(conversation.Id.ToString());
        Assert.Equal("Private customer message", Assert.Single(restored.Messages).Content);
        var entry = Assert.Single(logs.Events);
        Assert.Same(failure.InnerException, entry.Exception!.InnerException);
        Assert.DoesNotContain("Private customer message", entry.RenderMessage() + string.Join(" ", entry.Properties.Values));
        database.AssertAllContextsDisposed();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Requested_cancellation_before_or_during_operation_is_not_logged_and_releases_lock(bool cancelInAgent)
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        using var logs = new CapturedLogs();
        using var cancellation = new CancellationTokenSource();
        var agent = new CallbackAgent((_, token) =>
        {
            cancellation.Cancel();
            token.ThrowIfCancellationRequested();
            return Task.FromResult(new AgentChatResponse { Content = "Reply" });
        });
        var service = new ChatService(agent, new SqliteConversationRepository(database), new TestCurrentUser(), logs.Logger);
        var conversation = await service.StartConversationAsync();
        if (!cancelInAgent)
        {
            cancellation.Cancel();
        }

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.SendMessageAsync(conversation.Id.ToString(), "Private message", cancellation.Token));

        // A subsequent operation must still be able to acquire the semaphore.
        Assert.Equal(conversation.Id, (await service.GetConversationAsync(conversation.Id.ToString())
            .WaitAsync(TimeSpan.FromSeconds(5))).Id);
        Assert.Empty(logs.Events);
    }

    [Fact]
    public async Task Unrequested_cancellation_is_logged_as_a_failure()
    {
        using var logs = new CapturedLogs();
        var failure = new OperationCanceledException("Provider timeout");
        var service = new ChatService(new OfflineChatAgent(), new FailingRepository(failure), new TestCurrentUser(), logs.Logger);

        Assert.Same(failure, await Assert.ThrowsAsync<OperationCanceledException>(() => service.GetConversationsAsync()));

        Assert.Equal(LogEventLevel.Error, Assert.Single(logs.Events).Level);
    }

    [Fact]
    public async Task Invalid_conversation_id_is_not_recorded_as_arbitrary_input()
    {
        using var logs = new CapturedLogs();
        var service = new ChatService(new OfflineChatAgent(), new FailingRepository(new Exception()), new TestCurrentUser(), logs.Logger);

        await Assert.ThrowsAsync<ArgumentException>(() => service.GetConversationAsync("Private invalid input"));

        var entry = Assert.Single(logs.Events);
        Assert.Null(Assert.IsType<ScalarValue>(entry.Properties["ConversationId"]).Value);
        Assert.DoesNotContain("Private invalid input", entry.RenderMessage() + string.Join(" ", entry.Properties.Values) + entry.Exception);
    }

    private sealed class CapturedLogs : IDisposable
    {
        private readonly SerilogLoggerFactory factory;
        public List<LogEvent> Events { get; } = [];
        public ILogger<ChatService> Logger { get; }

        public CapturedLogs()
        {
            factory = new SerilogLoggerFactory(new LoggerConfiguration()
                .Enrich.FromLogContext().WriteTo.Sink(new CaptureSink(Events)).CreateLogger(), dispose: true);
            Logger = factory.CreateLogger<ChatService>();
        }

        public void Dispose() => factory.Dispose();
    }

    private sealed class CaptureSink(List<LogEvent> events) : ILogEventSink
    {
        public void Emit(LogEvent logEvent) => events.Add(logEvent);
    }

    private sealed class TestCurrentUser : ICurrentUser
    {
        public int UserId => 42;
    }

    private sealed class CallbackAgent(Func<IReadOnlyList<ChatMessage>, CancellationToken, Task<AgentChatResponse>> callback) : IChatAgent
    {
        public Task<AgentChatResponse> GetReplyAsync(IReadOnlyList<ChatMessage> history, CancellationToken cancellationToken = default) =>
            callback(history, cancellationToken);
    }

    private sealed class FailingRepository(Exception failure) : IConversationRepository
    {
        public Task AddAsync(Conversation conversation, int userId, CancellationToken cancellationToken = default) =>
            Task.FromException(failure);
        public Task<Conversation?> GetAsync(Guid conversationId, int userId, CancellationToken cancellationToken = default) =>
            Task.FromException<Conversation?>(failure);
        public Task<IReadOnlyList<Conversation>> GetAllAsync(int userId, CancellationToken cancellationToken = default) =>
            Task.FromException<IReadOnlyList<Conversation>>(failure);
        public Task<Conversation?> GetLatestActiveAsync(int userId, CancellationToken cancellationToken = default) =>
            Task.FromException<Conversation?>(failure);
        public Task SaveAsync(Conversation conversation, int userId, CancellationToken cancellationToken = default) =>
            Task.FromException(failure);
    }
}
