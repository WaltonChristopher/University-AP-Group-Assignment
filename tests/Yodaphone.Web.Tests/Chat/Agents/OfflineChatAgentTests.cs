using Xunit;
using Yodaphone.Web.Chat.Agents;
using Yodaphone.Web.Domain;

namespace Yodaphone.Web.Tests.Chat.Agents;

public sealed class OfflineChatAgentTests
{
    private readonly IChatAgent agent = new OfflineChatAgent();

    [Fact]
    public async Task GetReplyAsync_ReturnsTheOfflineResponse()
    {
        var conversation = CreateConversationWithCustomerMessage();

        var response = await agent.GetReplyAsync(
            conversation.Messages.ToList(),
            CancellationToken.None);

        Assert.Equal("This is an offline response.", response.Content);
    }

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
        var conversation = CreateConversationWithCustomerMessage();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => agent.GetReplyAsync(
                conversation.Messages.ToList(),
                cancellationTokenSource.Token));
    }

    private static Conversation CreateConversationWithCustomerMessage()
    {
        var conversation = new Conversation(Guid.NewGuid(), DateTimeOffset.UtcNow);
        conversation.AddMessage(MessageRole.Customer, "Hello");
        return conversation;
    }
}
