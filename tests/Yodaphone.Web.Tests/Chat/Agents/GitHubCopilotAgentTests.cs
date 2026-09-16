using Xunit;
using Yodaphone.Web.Chat.Agents;
using Yodaphone.Web.Domain;

namespace Yodaphone.Web.Tests.Chat.Agents;

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

        var conversation = new Conversation(Guid.NewGuid(), DateTimeOffset.UtcNow);

        var history = new[]
        {
            conversation.AddMessage(MessageRole.Customer, "Hello")
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => agent.GetReplyAsync(history, cancellationTokenSource.Token));
    }
}
