using GitHub.Copilot;
using Xunit;
using Yodaphone.Web.Chat.Agents.Copilot;
using Yodaphone.Web.Domain;
using Yodaphone.Web.Tests.Chat.Agents.TestDoubles;

namespace Yodaphone.Web.Tests.Chat.Agents;

/// <summary>
/// Exercises the real agent's workflow with fake SDK dependencies. These unit tests verify
/// orchestration and validation; they do not verify live Copilot authentication or connectivity.
/// The fake implementations live in TestDoubles so this file focuses on test scenarios.
/// </summary>
public sealed class GitHubCopilotAgentTests
{
    private readonly FakeCopilotClientFactory factory = new();
    private readonly GitHubCopilotAgent agent;

    public GitHubCopilotAgentTests()
    {
        // xUnit creates a new test-class instance for each case, giving each one fresh fake state.
        agent = new GitHubCopilotAgent(factory);
    }

    [Fact]
    public async Task GetReplyAsync_StartsClientCreatesSessionSendsPromptAndReturnsContent()
    {
        using var cancellationTokenSource = new CancellationTokenSource();

        var response = await agent.GetReplyAsync(CreateHistory(), cancellationTokenSource.Token);

        Assert.Equal("Copilot reply", response.Content);
        // Check the complete lifecycle, including disposal in reverse acquisition order.
        Assert.Equal(
            new[] { "create-client", "start", "create-session", "send", "dispose-session", "dispose-client" },
            factory.Calls);
        Assert.Equal(
            Enumerable.Repeat(cancellationTokenSource.Token, 3),
            factory.ReceivedTokens);

        Assert.NotNull(factory.Options);
        Assert.Equal(CopilotClientMode.Empty, factory.Options.Mode);
        Assert.False(string.IsNullOrWhiteSpace(factory.Options.BaseDirectory));

        Assert.NotNull(factory.SessionConfig);
        Assert.Equal("Yodaphone-web", factory.SessionConfig.ClientName);
        Assert.Equal("auto", factory.SessionConfig.Model);
        Assert.NotNull(factory.SessionConfig.AvailableTools);
        Assert.Empty(factory.SessionConfig.AvailableTools);
        Assert.NotNull(factory.SessionConfig.InfiniteSessions);
        Assert.False(factory.SessionConfig.InfiniteSessions.Enabled);
        Assert.NotNull(factory.SessionConfig.SystemMessage);
        Assert.Equal(SystemMessageMode.Append, factory.SessionConfig.SystemMessage.Mode);
        Assert.Contains("You are Yodaphone", factory.SessionConfig.SystemMessage.Content);

        Assert.NotNull(factory.MessageOptions);
        Assert.Equal(
            $"Customer: Hello{Environment.NewLine}Yodaphone assistant: ",
            factory.MessageOptions.Prompt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t\r\n")]
    public async Task GetReplyAsync_WithInvalidContent_ThrowsAndDisposesResources(string? content)
    {
        factory.Response = FakeCopilotClientFactory.CreateResponse(content);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => agent.GetReplyAsync(CreateHistory(), CancellationToken.None));

        Assert.Equal(new[] { "dispose-session", "dispose-client" }, factory.Calls.TakeLast(2));
    }

    [Fact]
    public async Task GetReplyAsync_WithNoResponse_ThrowsAndDisposesResources()
    {
        factory.Response = null;

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => agent.GetReplyAsync(CreateHistory(), CancellationToken.None));

        Assert.Equal(new[] { "dispose-session", "dispose-client" }, factory.Calls.TakeLast(2));
    }

    [Theory]
    [InlineData("start")]
    [InlineData("create-session")]
    [InlineData("send")]
    public async Task GetReplyAsync_WithSdkFailure_PropagatesAndDisposesAcquiredResources(string stage)
    {
        // Run this scenario at each SDK step. Later steps must not execute after a failure.
        var failure = new TimeoutException("SDK timed out.");
        factory.FailureStage = stage;
        factory.Failure = failure;

        var exception = await Assert.ThrowsAsync<TimeoutException>(
            () => agent.GetReplyAsync(CreateHistory(), CancellationToken.None));

        Assert.Same(failure, exception);
        AssertCallsThroughFailure(stage);
    }

    [Theory]
    [InlineData("start")]
    [InlineData("create-session")]
    [InlineData("send")]
    public async Task GetReplyAsync_WithCancellationDuringSdkCall_PropagatesAndDisposesAcquiredResources(
        string stage)
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        // Cancel inside the chosen SDK operation, rather than before input validation.
        factory.CancellationStage = stage;
        factory.CancelRequest = cancellationTokenSource.Cancel;

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => agent.GetReplyAsync(CreateHistory(), cancellationTokenSource.Token));

        Assert.Equal(cancellationTokenSource.Token, exception.CancellationToken);
        AssertCallsThroughFailure(stage);
    }

    [Fact]
    public void BuildTranscript_FormatsMessagesInOrderAndAddsAssistantPrompt()
    {
        var conversation = new Conversation(Guid.NewGuid(), DateTimeOffset.UtcNow);
        var history = new[]
        {
            conversation.AddMessage(MessageRole.Customer, "I need help"),
            conversation.AddMessage(MessageRole.Assistant, "How can I help?"),
            conversation.AddMessage(MessageRole.System, "Escalation available")
        };

        var transcript = GitHubCopilotAgent.BuildTranscript(history);

        var expected = string.Join(
            Environment.NewLine,
            "Customer: I need help",
            "Assistant: How can I help?",
            "System: Escalation available",
            "Yodaphone assistant: ");
        Assert.Equal(expected, transcript);
    }

    [Fact]
    public async Task GetReplyAsync_WithNullHistory_ThrowsArgumentNullException()
    {
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(
            () => agent.GetReplyAsync(null!, CancellationToken.None));

        Assert.Equal("history", exception.ParamName);
        // Invalid requests should be rejected before even constructing a client.
        Assert.Empty(factory.Calls);
    }

    [Fact]
    public async Task GetReplyAsync_WithEmptyHistory_ThrowsArgumentException()
    {
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => agent.GetReplyAsync([], CancellationToken.None));

        Assert.Equal("history", exception.ParamName);
        Assert.Empty(factory.Calls);
    }

    [Fact]
    public async Task GetReplyAsync_WithCancellationRequested_ThrowsOperationCanceledException()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        var conversation = new Conversation(Guid.NewGuid(), DateTimeOffset.UtcNow);

        var history = new[]
        {
            conversation.AddMessage(MessageRole.Customer, "Hello")
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => agent.GetReplyAsync(history, cancellationTokenSource.Token));
        Assert.Empty(factory.Calls);
    }

    private void AssertCallsThroughFailure(string stage)
    {
        // Startup/session-creation failures have no acquired session to dispose.
        // A send failure has both resources, so the session must be disposed before the client.
        string[] expected = stage switch
        {
            "start" => ["create-client", "start", "dispose-client"],
            "create-session" => ["create-client", "start", "create-session", "dispose-client"],
            "send" => ["create-client", "start", "create-session", "send", "dispose-session", "dispose-client"],
            _ => throw new ArgumentException("Unknown SDK stage.", nameof(stage))
        };
        Assert.Equal(expected, factory.Calls);
    }

    private static IReadOnlyList<ChatMessage> CreateHistory()
    {
        var conversation = new Conversation(Guid.NewGuid(), DateTimeOffset.UtcNow);
        return [conversation.AddMessage(MessageRole.Customer, "Hello")];
    }
}
