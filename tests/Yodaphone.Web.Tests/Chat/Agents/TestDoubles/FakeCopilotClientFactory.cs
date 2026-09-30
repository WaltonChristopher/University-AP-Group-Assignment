using GitHub.Copilot;
using Yodaphone.Web.Chat.Agents.Copilot;

namespace Yodaphone.Web.Tests.Chat.Agents.TestDoubles;

/// <summary>
/// Supplies client and session replacements for testing, so no real Copilot runtime is started.
/// Each test creates its own factory so recorded calls and configured failures stay isolated.
/// </summary>
internal sealed class FakeCopilotClientFactory : ICopilotClientFactory
{
    // Observations: tests inspect these to check call order, configuration, and token forwarding.
    public List<string> Calls { get; } = [];
    public List<CancellationToken> ReceivedTokens { get; } = [];
    public CopilotClientOptions? Options { get; private set; }
    public SessionConfig? SessionConfig { get; private set; }
    public MessageOptions? MessageOptions { get; private set; }

    // Controls: a test can choose a reply, fail one SDK stage, or cancel during that stage.
    // Stage names match the recorded calls: "start", "create-session", or "send".
    public AssistantMessageEvent? Response { get; set; } = CreateResponse("Copilot reply");
    public string? FailureStage { get; set; }
    public Exception? Failure { get; set; }
    public string? CancellationStage { get; set; }
    public Action? CancelRequest { get; set; }

    public ICopilotClient Create(CopilotClientOptions options)
    {
        Calls.Add("create-client");
        Options = options;
        return new FakeClient(this);
    }

    /// <summary>
    /// Builds an SDK-shaped reply. Null content is deliberately allowed so tests can
    /// verify the agent rejects malformed replies despite the SDK's non-nullable property.
    /// </summary>
    public static AssistantMessageEvent CreateResponse(string? content)
    {
        return new AssistantMessageEvent
        {
            Data = new AssistantMessageData { MessageId = "test-message", Content = content! }
        };
    }

    private async Task CompleteStageAsync(string stage, CancellationToken cancellationToken)
    {
        // Yield to exercise asynchronous execution rather than always returning completed tasks.
        await Task.Yield();
        Calls.Add(stage);
        ReceivedTokens.Add(cancellationToken);
        if (stage == CancellationStage)
        {
            // The test supplies CancellationTokenSource.Cancel to cancel the caller's token
            // after input validation, while an SDK operation is in progress.
            CancelRequest!();
        }
        cancellationToken.ThrowIfCancellationRequested();
        if (stage == FailureStage)
        {
            // Throw the supplied instance so the test can check that the agent preserves it.
            throw Failure!;
        }
    }

    // These helpers share the factory's recording state so one list captures the full lifecycle.
    // They are nested because only the factory creates them; tests use the controls above.
    private sealed class FakeClient(FakeCopilotClientFactory owner) : ICopilotClient
    {
        public Task StartAsync(CancellationToken cancellationToken)
        {
            return owner.CompleteStageAsync("start", cancellationToken);
        }

        public async Task<ICopilotSession> CreateSessionAsync(
            SessionConfig config,
            CancellationToken cancellationToken)
        {
            owner.SessionConfig = config;
            await owner.CompleteStageAsync("create-session", cancellationToken);
            return new FakeSession(owner);
        }

        public ValueTask DisposeAsync()
        {
            owner.Calls.Add("dispose-client");
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakeSession(FakeCopilotClientFactory owner) : ICopilotSession
    {
        public async Task<AssistantMessageEvent?> SendAndWaitAsync(
            MessageOptions options,
            CancellationToken cancellationToken)
        {
            owner.MessageOptions = options;
            await owner.CompleteStageAsync("send", cancellationToken);
            return owner.Response;
        }

        public ValueTask DisposeAsync()
        {
            // There is no external resource to release; recording disposal lets tests verify ownership.
            owner.Calls.Add("dispose-session");
            return ValueTask.CompletedTask;
        }
    }
}
