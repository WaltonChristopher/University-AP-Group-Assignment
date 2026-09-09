using Xunit;

public sealed class GitHubCopilotAgentTests
{
    private readonly GitHubCopilotAgent agent = new();

    [Fact]
    public async Task GetReplyAsync_WithNullHistory_ThrowsArgumentNullException()
    {
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(
            () => agent.GetReplyAsync(null!, CancellationToken.None));

        Assert.Equal("history", exception.ParamName);
    }

    [Fact]
    public async Task GetReplyAsync_WithEmptyHistory_ThrowsArgumentException()
    {
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => agent.GetReplyAsync([], CancellationToken.None));

        Assert.Equal("history", exception.ParamName);
    }

    [Fact]
    public async Task GetReplyAsync_WithCancellationRequested_ThrowsOperationCanceledException()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();
        var history = new[]
        {
            new ChatMessage { Role = "Customer", Content = "Hello" }
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => agent.GetReplyAsync(history, cancellationTokenSource.Token));
    }
}
