using Xunit;
using Yodaphone.Web.Chat;
using Yodaphone.Web.Chat.Agents;
using Yodaphone.Web.Domain;

namespace Yodaphone.Web.Tests.Chat.Agents;

public sealed class ChatAgentBaseTests
{
    [Fact]
    public async Task GetReplyAsync_WithValidRequest_DelegatesToCoreAndReturnsItsResponse()
    {
        var expectedResponse = new AgentChatResponse
        {
            Content = "A response"
        };
        var agent = new RecordingChatAgent(expectedResponse);
        var history = CreateHistory();
        using var cancellationTokenSource = new CancellationTokenSource();

        var response = await agent.GetReplyAsync(history, cancellationTokenSource.Token);

        Assert.Same(expectedResponse, response);
        Assert.Equal(1, agent.CoreCallCount);
        Assert.Same(history, agent.ReceivedHistory);
        Assert.Equal(cancellationTokenSource.Token, agent.ReceivedCancellationToken);
    }

    [Fact]
    public async Task GetReplyAsync_WithNullHistory_ThrowsAndDoesNotCallCore()
    {
        var agent = new RecordingChatAgent();

        var exception = await Assert.ThrowsAsync<ArgumentNullException>(
            () => agent.GetReplyAsync(null!, CancellationToken.None));

        Assert.Equal("history", exception.ParamName);
        Assert.Equal(0, agent.CoreCallCount);
    }

    [Fact]
    public async Task GetReplyAsync_WithEmptyHistory_ThrowsAndDoesNotCallCore()
    {
        var agent = new RecordingChatAgent();

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => agent.GetReplyAsync([], CancellationToken.None));

        Assert.Equal("history", exception.ParamName);
        Assert.Equal(0, agent.CoreCallCount);
    }

    [Fact]
    public async Task GetReplyAsync_WithCancelledToken_ThrowsAndDoesNotCallCore()
    {
        var agent = new RecordingChatAgent();
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        var exception = await Assert.ThrowsAsync<OperationCanceledException>(
            () => agent.GetReplyAsync(CreateHistory(), cancellationTokenSource.Token));

        Assert.Equal(cancellationTokenSource.Token, exception.CancellationToken);
        Assert.Equal(0, agent.CoreCallCount);
    }

    [Fact]
    public async Task GetReplyAsync_WithInvalidHistoryAndCancelledToken_ValidatesHistoryFirst()
    {
        var agent = new RecordingChatAgent();
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => agent.GetReplyAsync([], cancellationTokenSource.Token));

        Assert.Equal("history", exception.ParamName);
        Assert.Equal(0, agent.CoreCallCount);
    }

    private static IReadOnlyList<ChatMessage> CreateHistory()
    {
        var conversation = new Conversation(Guid.NewGuid(), DateTimeOffset.UtcNow);
        return [conversation.AddMessage(MessageRole.Customer, "Hello")];
    }

    private sealed class RecordingChatAgent : ChatAgentBase
    {
        private readonly AgentChatResponse response;

        public RecordingChatAgent(AgentChatResponse? response = null)
        {
            this.response = response ?? new AgentChatResponse
            {
                Content = "Default response"
            };
        }

        public int CoreCallCount { get; private set; }
        public IReadOnlyList<ChatMessage>? ReceivedHistory { get; private set; }
        public CancellationToken ReceivedCancellationToken { get; private set; }

        protected override Task<AgentChatResponse> GetReplyCoreAsync(
            IReadOnlyList<ChatMessage> history,
            CancellationToken cancellationToken)
        {
            CoreCallCount++;
            ReceivedHistory = history;
            ReceivedCancellationToken = cancellationToken;
            return Task.FromResult(response);
        }
    }
}
