using Xunit;
using Yodaphone.Web.Chat.Agents;

namespace Yodaphone.Web.Tests.Chat.Agents;

public sealed class ChatAgentContractTests
{
    [Fact]
    public void Implementations_InheritFromChatAgentBase()
    {
        var implementations = typeof(IChatAgent).Assembly
            .GetTypes()
            .Where(type =>
                type is { IsClass: true, IsAbstract: false }
                && typeof(IChatAgent).IsAssignableFrom(type))
            .ToList();

        Assert.NotEmpty(implementations);
        Assert.All(
            implementations,
            implementation => Assert.True(
                typeof(ChatAgentBase).IsAssignableFrom(implementation),
                $"{implementation.Name} must inherit from {nameof(ChatAgentBase)}."));
    }
}
