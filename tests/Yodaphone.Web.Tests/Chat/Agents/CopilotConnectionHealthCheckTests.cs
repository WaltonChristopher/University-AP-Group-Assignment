using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;
using Yodaphone.Web.Chat.Agents.Copilot;
using Yodaphone.Web.Tests.Chat.Agents.TestDoubles;

namespace Yodaphone.Web.Tests.Chat.Agents;

public sealed class CopilotConnectionHealthCheckTests
{
    private readonly FakeCopilotClientFactory factory = new();
    private readonly TestTimeProvider timeProvider = new();

    [Fact]
    public async Task Check_OnlyRequestsMetadataWithoutCreatingASessionOrSendingPrompts()
    {
        Assert.True(await CreateCheck().IsAvailableAsync(CancellationToken.None));

        Assert.Equal(new[] { "create-client", "start", "list-models", "dispose-client" }, factory.Calls);
        Assert.Null(factory.SessionConfig);
        Assert.Null(factory.MessageOptions);
        Assert.All(factory.ReceivedTokens, token => Assert.True(token.CanBeCanceled));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Check_CachesSuccessAndFailureAcrossConcurrentCallersUntilTheNextInterval(bool succeeds)
    {
        if (!succeeds)
        {
            factory.FailureStage = "list-models";
            factory.Failure = new IOException("Service unavailable");
        }
        var check = CreateCheck();

        var results = await Task.WhenAll(Enumerable.Range(0, 20)
            .Select(_ => check.IsAvailableAsync(CancellationToken.None)));

        Assert.All(results, result => Assert.Equal(succeeds, result));
        Assert.Single(factory.Calls, call => call == "create-client");
        timeProvider.Advance(TimeSpan.FromSeconds(29));
        Assert.Equal(succeeds, await check.IsAvailableAsync(CancellationToken.None));
        Assert.Single(factory.Calls, call => call == "create-client");

        timeProvider.Advance(TimeSpan.FromSeconds(1));
        factory.FailureStage = null;
        Assert.True(await check.IsAvailableAsync(CancellationToken.None));
        Assert.Equal(2, factory.Calls.Count(call => call == "create-client"));
    }

    [Theory]
    [InlineData("create-client")]
    [InlineData("start")]
    [InlineData("list-models")]
    [InlineData("dispose-client")]
    public async Task Check_FailedSdkStageReportsUnavailable(string stage)
    {
        factory.FailureStage = stage;
        factory.Failure = new IOException("private connection details");

        Assert.False(await CreateCheck().IsAvailableAsync(CancellationToken.None));
        Assert.DoesNotContain("create-session", factory.Calls);
        Assert.DoesNotContain("send", factory.Calls);
    }

    [Fact]
    public async Task Check_EmptyModelsReportsUnavailable()
    {
        factory.Models = [];

        Assert.False(await CreateCheck().IsAvailableAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Check_TimeoutDoesNotWaitForSdkOrAccumulateClientsAndDiscardsLateSuccess()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var disposed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        factory.OnStageAsync = (stage, _) => stage == "list-models" ? gate.Task : Task.CompletedTask;
        factory.OnClientDisposed = () => disposed.TrySetResult();
        var check = CreateCheck(timeoutSeconds: 1);

        try
        {
            Assert.False(await check.IsAvailableAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.All(factory.ReceivedTokens, token => Assert.True(token.IsCancellationRequested));

            timeProvider.Advance(TimeSpan.FromSeconds(10));
            Assert.False(await check.IsAvailableAsync(CancellationToken.None));
            Assert.Single(factory.Calls, call => call == "create-client");
        }
        finally
        {
            gate.TrySetResult();
            await disposed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }

        factory.OnStageAsync = null;
        Assert.True(await check.IsAvailableAsync(CancellationToken.None));
        Assert.Equal(2, factory.Calls.Count(call => call == "create-client"));
    }

    [Fact]
    public async Task Check_CallerCancellationIsNotCachedAndLaterCheckCanSucceed()
    {
        using var cancellation = new CancellationTokenSource();
        var disposed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        factory.OnClientDisposed = () => disposed.TrySetResult();
        factory.CancellationStage = "list-models";
        factory.CancelRequest = cancellation.Cancel;
        var check = CreateCheck();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => check.IsAvailableAsync(cancellation.Token));
        await disposed.Task.WaitAsync(TimeSpan.FromSeconds(10));

        factory.CancellationStage = null;
        Assert.True(await check.IsAvailableAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Check_PreCancelledRequestDoesNotStartTheSdk()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateCheck().IsAvailableAsync(cancellation.Token));

        Assert.Empty(factory.Calls);
    }

    private CopilotConnectionHealthCheck CreateCheck(int timeoutSeconds = 30) => new(
        factory,
        Options.Create(new CopilotOptions { ResponseTimeoutSeconds = timeoutSeconds }),
        NullLogger<CopilotConnectionHealthCheck>.Instance,
        timeProvider);

    private sealed class TestTimeProvider : TimeProvider
    {
        private DateTimeOffset now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => now;
        public void Advance(TimeSpan interval) => now += interval;
    }
}
