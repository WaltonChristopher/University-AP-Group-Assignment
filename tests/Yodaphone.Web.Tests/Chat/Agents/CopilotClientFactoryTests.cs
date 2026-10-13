using GitHub.Copilot;
using Microsoft.Extensions.Options;
using Xunit;
using Yodaphone.Web.Chat.Agents.Copilot;

namespace Yodaphone.Web.Tests.Chat.Agents;

public sealed class CopilotClientFactoryTests
{
    [Fact]
    public void Agent_DefaultConstructorCreatesAgentWithoutStartingRuntime() =>
        Assert.IsAssignableFrom<Yodaphone.Web.Chat.Agents.IChatAgent>(new GitHubCopilotAgent());

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t")]
    [InlineData("test-token")]
    public async Task Create_UsesServerCredentialOrLocalLogin(string? token)
    {
        var factory = new CopilotClientFactory(Options.Create(new CopilotOptions { GitHubToken = token }));
        var options = new CopilotClientOptions { GitHubToken = "untrusted-token" };
        await using var client = factory.Create(options);
        var expected = string.IsNullOrWhiteSpace(token) ? null : token;
        Assert.Equal(expected, options.GitHubToken);
        Assert.Equal(expected is null, options.UseLoggedInUser);
    }

    [Fact]
    public async Task Create_DefaultFactoryUsesLocalLogin()
    {
        var options = new CopilotClientOptions();
        await using var client = new CopilotClientFactory().Create(options);
        Assert.Null(options.GitHubToken);
        Assert.True(options.UseLoggedInUser);
    }

    [Fact]
    public void Create_RejectsNullOptions() =>
        Assert.Throws<ArgumentNullException>(() => new CopilotClientFactory().Create(null!));
}
