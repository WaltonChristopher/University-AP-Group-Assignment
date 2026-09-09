using Xunit;

public sealed class OfflineChatAgentTests
{
    [Fact]
    public async Task GetReplyAsync_ReturnsTheOfflineResponse()
    {
        IChatAgent agent = new OfflineChatAgent();

        var response = await agent.GetReplyAsync([], CancellationToken.None);

        Assert.Equal("This is an offline response.", response.Content);
    }
}
