using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;
using Yodaphone.Web.Chat.Agents;
using Yodaphone.Web.Chat.Agents.Copilot;
using Yodaphone.Web.Components;
using Yodaphone.Web.Data;
using Yodaphone.Web.Services;

namespace Yodaphone.Web.Tests.Hosting;

public sealed class ApplicationHostingTests
{
    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task Startup_MigratesDatabaseAndServesChat(string environment)
    {
        await using var factory = new TestApplicationFactory(environment, "  OFFLINE  ");
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("How can I help with your account today?", await response.Content.ReadAsStringAsync());
        using var scope = factory.Services.CreateScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
        await using var db = await dbFactory.CreateDbContextAsync();
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.Equal(3, (await db.Database.GetAppliedMigrationsAsync()).Count());
        Assert.Single(await db.Conversations.ToListAsync());
        Assert.Equal(42, (await db.Users.SingleAsync()).UserId);
        Assert.IsType<OfflineChatAgent>(scope.ServiceProvider.GetRequiredService<IChatAgent>());
        Assert.IsType<ChatService>(scope.ServiceProvider.GetRequiredService<IChatService>());
    }

    [Fact]
    public async Task MissingRoute_ReturnsNotFoundPage()
    {
        await using var factory = new TestApplicationFactory("Production", "offline");
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/does-not-exist");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("Not Found", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ErrorRoute_DisplaysRequestIdentifier()
    {
        await using var factory = new TestApplicationFactory("Production", "offline");
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/Error");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("An error occurred while processing your request.", html);
        Assert.Contains("Request ID:", html);
    }

    [Fact]
    public async Task Startup_CopilotConfigurationRegistersAgentAndServerOptions()
    {
        await using var factory = new TestApplicationFactory("Production", " COPILOT ");
        using var scope = factory.Services.CreateScope();
        Assert.IsType<GitHubCopilotAgent>(scope.ServiceProvider.GetRequiredService<IChatAgent>());
        Assert.IsType<CopilotClientFactory>(scope.ServiceProvider.GetRequiredService<ICopilotClientFactory>());
        Assert.Equal("test-server-token", scope.ServiceProvider.GetRequiredService<IOptions<CopilotOptions>>().Value.GitHubToken);
    }

    [Fact]
    public async Task Startup_RejectsUnknownAgent()
    {
        await using var factory = new TestApplicationFactory("Production", "unsupported");
        var exception = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
        Assert.Contains("Unknown chat agent: unsupported", exception.Message);
    }

    private sealed class TestApplicationFactory(string environment, string agent) : WebApplicationFactory<App>
    {
        private readonly string databasePath = Path.Combine(Path.GetTempPath(), $"yodaphone-tests-{Guid.NewGuid():N}.db");

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(environment);
            builder.UseSetting("Chat:Agent", agent);
            builder.UseSetting("Chat:DevelopmentUserId", "42");
            builder.UseSetting("Chat:Copilot:GitHubToken", "test-server-token");
            builder.UseSetting("ConnectionStrings:DefaultConnection", $"Data Source={databasePath};Pooling=False");
        }

        public override async ValueTask DisposeAsync()
        {
            await base.DisposeAsync();
            foreach (var suffix in new[] { "", "-wal", "-shm" }) File.Delete(databasePath + suffix);
        }
    }
}
