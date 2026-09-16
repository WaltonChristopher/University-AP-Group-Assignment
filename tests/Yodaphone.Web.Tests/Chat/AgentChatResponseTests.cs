using Xunit;
using Yodaphone.Web.Chat;

namespace Yodaphone.Web.Tests.Chat;

public sealed class AgentChatResponseTests
{
    [Fact]
    public void Content_CanBeInitializedAndUpdated()
    {
        var response = new AgentChatResponse
        {
            Content = "Initial response"
        };

        Assert.Equal("Initial response", response.Content);

        response.Content = "Updated response";

        Assert.Equal("Updated response", response.Content);
    }
}
