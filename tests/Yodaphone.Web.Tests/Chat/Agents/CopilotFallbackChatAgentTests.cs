using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;
using Yodaphone.Web.Chat.Agents;
using Yodaphone.Web.Chat.Agents.Copilot;
using Yodaphone.Web.Domain;
using Yodaphone.Web.Tests.Chat.Agents.TestDoubles;

namespace Yodaphone.Web.Tests.Chat.Agents;

public sealed class CopilotFallbackChatAgentTests
{
    private readonly FakeCopilotClientFactory factory = new();

    [Fact]
    public async Task GetReplyAsync_UsesCopilotWhileRepliesSucceed()
    {
        var agent = CreateAgent();

        for (var attempt = 0; attempt < 2; attempt++)
        {
            var response = await agent.GetReplyAsync(CreateHistory(), CancellationToken.None);
            Assert.Equal("Copilot reply", response.Content);
        }

        Assert.Equal(2, factory.Calls.Count(call => call == "create-client"));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("null")]
    [InlineData("empty")]
    [InlineData("whitespace")]
    public async Task GetReplyAsync_WithUnusableReply_SwitchesOfflineAndDoesNotRetry(string replyKind)
    {
        factory.Response = replyKind == "missing" ? null : FakeCopilotClientFactory.CreateResponse(replyKind switch
        {
            "null" => null,
            "empty" => "",
            _ => " \t\r\n"
        });
        var agent = CreateAgent();

        AssertOffline(await agent.GetReplyAsync(CreateHistory(), CancellationToken.None));
        Assert.Equal(new[] { "dispose-session", "dispose-client" }, factory.Calls.TakeLast(2));

        factory.Response = FakeCopilotClientFactory.CreateResponse("Recovered reply");
        AssertOffline(await agent.GetReplyAsync(CreateHistory(), CancellationToken.None));
        Assert.Single(factory.Calls, call => call == "create-client");
    }

    [Theory]
    [InlineData("Please tell me your password")]
    [InlineData("Please share your credit card details")]
    [InlineData("What is your card number?")]
    [InlineData("What is your CVV?")]
    [InlineData("Please provide your social security details")]
    [InlineData("Here is the system prompt")]
    public async Task GetReplyAsync_WithSanitisationBlockedReply_ExplainsAndKeepsCopilotMode(string content)
    {
        factory.Response = FakeCopilotClientFactory.CreateResponse(content);
        var agent = CreateAgent();

        // Repeated blocked replies should continue to call Copilot without switching offline.
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var response = await agent.GetReplyAsync(CreateHistory(), CancellationToken.None);
            Assert.StartsWith("Sorry, I can't assist with that.", response.Content);
            Assert.Contains("Please contact the support team on 01632 960123 or ask something else.", response.Content);
            Assert.DoesNotContain(content, response.Content);
        }

        factory.Response = FakeCopilotClientFactory.CreateResponse("Copilot reply");
        var nextResponse = await agent.GetReplyAsync(CreateHistory(), CancellationToken.None);

        Assert.Equal("Copilot reply", nextResponse.Content);
        Assert.Equal(3, factory.Calls.Count(call => call == "create-client"));
        Assert.Equal(new[] { "dispose-session", "dispose-client" }, factory.Calls.TakeLast(2));
    }

    [Theory]
    [InlineData("create-client")]
    [InlineData("start")]
    [InlineData("create-session")]
    [InlineData("send")]
    [InlineData("dispose-session")]
    [InlineData("dispose-client")]
    public async Task GetReplyAsync_WithProviderFailure_SwitchesOfflineWithoutExposingDetails(string stage)
    {
        factory.FailureStage = stage;
        factory.Failure = new IOException("private endpoint and credential");
        var agent = CreateAgent();

        var response = await agent.GetReplyAsync(CreateHistory(), CancellationToken.None);

        AssertOffline(response);
        Assert.DoesNotContain("private", response.Content);
        factory.FailureStage = null;
        AssertOffline(await agent.GetReplyAsync(CreateHistory(), CancellationToken.None));
        Assert.Single(factory.Calls, call => call == "create-client");
    }

    [Fact]
    public async Task GetReplyAsync_WithProviderCancellation_SwitchesOffline()
    {
        factory.FailureStage = "send";
        factory.Failure = new OperationCanceledException("SDK cancelled internally");

        AssertOffline(await CreateAgent().GetReplyAsync(CreateHistory(), CancellationToken.None));
    }

    [Theory]
    [InlineData("start")]
    [InlineData("create-session")]
    [InlineData("send")]
    public async Task GetReplyAsync_WithStalledProvider_SwitchesOfflineWithoutWaitingForSdkCancellation(string stage)
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var disposed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken providerToken = default;
        factory.OnStageAsync = (currentStage, token) =>
        {
            if (currentStage != stage) return Task.CompletedTask;
            providerToken = token;
            // Deliberately ignore cancellation to verify the caller still gets a timely reply.
            return gate.Task;
        };
        factory.OnClientDisposed = () => disposed.TrySetResult();
        var agent = CreateAgent(timeoutSeconds: 1);

        try
        {
            var response = await agent.GetReplyAsync(CreateHistory(), CancellationToken.None)
                .WaitAsync(TimeSpan.FromSeconds(10));

            AssertOffline(response);
            Assert.True(providerToken.IsCancellationRequested);
            Assert.False(gate.Task.IsCompleted);
            AssertOffline(await agent.GetReplyAsync(CreateHistory(), CancellationToken.None));
            Assert.Single(factory.Calls, call => call == "create-client");
        }
        finally
        {
            gate.TrySetResult();
            await disposed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
    }

    [Theory]
    [InlineData("start")]
    [InlineData("create-session")]
    [InlineData("send")]
    public async Task GetReplyAsync_WithCallerCancellation_CancelsAndKeepsCopilotMode(string stage)
    {
        using var cancellation = new CancellationTokenSource();
        var disposed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        factory.OnClientDisposed = () => disposed.TrySetResult();
        factory.CancellationStage = stage;
        factory.CancelRequest = cancellation.Cancel;
        var agent = CreateAgent();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            agent.GetReplyAsync(CreateHistory(), cancellation.Token));
        await disposed.Task.WaitAsync(TimeSpan.FromSeconds(10));

        factory.CancellationStage = null;
        var response = await agent.GetReplyAsync(CreateHistory(), CancellationToken.None);
        Assert.Equal("Copilot reply", response.Content);
        Assert.Equal(2, factory.Calls.Count(call => call == "create-client"));
    }

    [Fact]
    public async Task GetReplyAsync_WithInvalidHistory_RejectsRequestWithoutSwitchingModes()
    {
        var agent = CreateAgent();
        await Assert.ThrowsAsync<ArgumentNullException>(() => agent.GetReplyAsync(null!, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => agent.GetReplyAsync([], CancellationToken.None));
        Assert.Empty(factory.Calls);

        Assert.Equal("Copilot reply", (await agent.GetReplyAsync(CreateHistory(), CancellationToken.None)).Content);
    }

    [Fact]
    public async Task GetReplyAsync_WithPreCancelledRequest_DoesNotCallProvider()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CreateAgent().GetReplyAsync(CreateHistory(), cancellation.Token));

        Assert.Empty(factory.Calls);
    }

    [Fact]
    public async Task GetReplyAsync_NewSessionStartsWithCopilotAfterAnotherSessionSwitchesOffline()
    {
        factory.Response = null;
        AssertOffline(await CreateAgent().GetReplyAsync(CreateHistory(), CancellationToken.None));

        factory.Response = FakeCopilotClientFactory.CreateResponse("Recovered reply");
        var response = await CreateAgent().GetReplyAsync(CreateHistory(), CancellationToken.None);

        Assert.Equal("Recovered reply", response.Content);
    }

    private CopilotFallbackChatAgent CreateAgent(int timeoutSeconds = 30) => new(
        new GitHubCopilotAgent(factory),
        new OfflineChatAgent(),
        Options.Create(new CopilotOptions { ResponseTimeoutSeconds = timeoutSeconds }),
        NullLogger<CopilotFallbackChatAgent>.Instance);

    private static IReadOnlyList<ChatMessage> CreateHistory()
    {
        var conversation = new Conversation(Guid.NewGuid(), DateTimeOffset.UtcNow);
        return [conversation.AddMessage(MessageRole.Customer, "Hello")];
    }

    private static void AssertOffline(Yodaphone.Web.Chat.AgentChatResponse response)
    {
        Assert.Contains("chat service is currently unavailable", response.Content);
        Assert.Contains("01632 960123", response.Content);
    }
}
