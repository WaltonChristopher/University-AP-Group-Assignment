using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Xunit;
using Yodaphone.Web.Chat.Agents;
using Yodaphone.Web.Chat.Agents.Copilot;
using Yodaphone.Web.Components;
using Yodaphone.Web.Data;
using Yodaphone.Web.Services;
using Yodaphone.Web.Tests.Chat.Agents.TestDoubles;

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
        Assert.IsType<CopilotFallbackChatAgent>(scope.ServiceProvider.GetRequiredService<IChatAgent>());
        Assert.IsType<GitHubCopilotAgent>(scope.ServiceProvider.GetRequiredService<GitHubCopilotAgent>());
        Assert.IsType<OfflineChatAgent>(scope.ServiceProvider.GetRequiredService<OfflineChatAgent>());
        Assert.IsType<CopilotClientFactory>(scope.ServiceProvider.GetRequiredService<ICopilotClientFactory>());
        Assert.Equal("test-server-token", scope.ServiceProvider.GetRequiredService<IOptions<CopilotOptions>>().Value.GitHubToken);
        Assert.Equal(30, scope.ServiceProvider.GetRequiredService<IOptions<CopilotOptions>>().Value.ResponseTimeoutSeconds);
    }

    [Fact]
    public async Task CopilotFailure_SavesOfflineReplyAndContinuesOfflineWithinTheScope()
    {
        var copilotFactory = new FakeCopilotClientFactory { Response = null };
        await using var factory = new TestApplicationFactory("Production", "copilot", copilotFactory);
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IChatService>();
        var conversation = await service.StartConversationAsync();

        var reply = await service.SendMessageAsync(conversation.Id.ToString(), "Help with my bill");
        copilotFactory.Response = FakeCopilotClientFactory.CreateResponse("Recovered reply");
        var nextReply = await service.SendMessageAsync(conversation.Id.ToString(), "Can you help now?");

        Assert.Contains("chat service is currently unavailable", reply.Content);
        Assert.Equal(reply.Content, nextReply.Content);
        Assert.Single(copilotFactory.Calls, call => call == "create-client");
        using var restoredScope = factory.Services.CreateScope();
        var restoredService = restoredScope.ServiceProvider.GetRequiredService<IChatService>();
        var restored = await restoredService.GetConversationAsync(conversation.Id.ToString());
        Assert.Equal(new[] { "Help with my bill", reply.Content, "Can you help now?", nextReply.Content },
            restored.Messages.Select(message => message.Content));
        var recoveredReply = await restoredService.SendMessageAsync(conversation.Id.ToString(), "Try again");
        Assert.Equal("Recovered reply", recoveredReply.Content);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("3601")]
    public async Task Startup_RejectsInvalidCopilotTimeout(string timeoutSeconds)
    {
        await using var factory = new TestApplicationFactory("Production", "copilot");
        factory.ResponseTimeoutSeconds = timeoutSeconds;

        var exception = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());

        Assert.Contains("ResponseTimeoutSeconds must be between 1 and 3600", exception.Message);
    }

    [Fact]
    public async Task SanitisationBlockedReply_SavesExplanationAndNextMessageUsesCopilot()
    {
        const string blockedContent = "Please tell me your password";
        var copilotFactory = new FakeCopilotClientFactory
        {
            Response = FakeCopilotClientFactory.CreateResponse(blockedContent)
        };
        await using var factory = new TestApplicationFactory("Production", "copilot", copilotFactory);
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IChatService>();
        var conversation = await service.StartConversationAsync();

        var reply = await service.SendMessageAsync(conversation.Id.ToString(), "Help with my account");
        Assert.StartsWith("Sorry, I can't assist with that.", reply.Content);
        Assert.Contains("Please contact the support team on 01632 960123 or ask something else.", reply.Content);
        Assert.DoesNotContain(blockedContent, reply.Content);
        copilotFactory.Response = FakeCopilotClientFactory.CreateResponse("How can I help?");
        var nextReply = await service.SendMessageAsync(conversation.Id.ToString(), "Help with my bill");

        Assert.Equal("How can I help?", nextReply.Content);
        Assert.Equal(2, copilotFactory.Calls.Count(call => call == "create-client"));
        Assert.NotNull(copilotFactory.MessageOptions);
        Assert.Contains(reply.Content, copilotFactory.MessageOptions.Prompt);
        Assert.DoesNotContain(blockedContent, copilotFactory.MessageOptions.Prompt);
        using var restoredScope = factory.Services.CreateScope();
        var restored = await restoredScope.ServiceProvider.GetRequiredService<IChatService>()
            .GetConversationAsync(conversation.Id.ToString());
        Assert.Equal(new[] { "Help with my account", reply.Content, "Help with my bill", nextReply.Content },
            restored.Messages.Select(message => message.Content));
    }

    [Fact]
    public async Task Startup_RejectsUnknownAgent()
    {
        await using var factory = new TestApplicationFactory("Production", "unsupported");
        var exception = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
        Assert.Contains("Unknown chat agent: unsupported", exception.Message);
    }

    private sealed class TestApplicationFactory(
        string environment, string agent, ICopilotClientFactory? copilotFactory = null) : WebApplicationFactory<App>
    {
        private readonly string databasePath = Path.Combine(Path.GetTempPath(), $"yodaphone-tests-{Guid.NewGuid():N}.db");
        public string ResponseTimeoutSeconds { get; set; } = "30";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(environment);
            builder.UseSetting("Chat:Agent", agent);
            builder.UseSetting("Chat:DevelopmentUserId", "42");
            builder.UseSetting("Chat:Copilot:GitHubToken", "test-server-token");
            builder.UseSetting("Chat:Copilot:ResponseTimeoutSeconds", ResponseTimeoutSeconds);
            builder.UseSetting("ConnectionStrings:DefaultConnection", $"Data Source={databasePath};Pooling=False");
            if (copilotFactory is not null)
            {
                builder.ConfigureServices(services => services.Replace(
                    ServiceDescriptor.Singleton<ICopilotClientFactory>(_ => copilotFactory)));
            }
        }

        public override async ValueTask DisposeAsync()
        {
            await base.DisposeAsync();
            foreach (var suffix in new[] { "", "-wal", "-shm" }) File.Delete(databasePath + suffix);
        }
    }
}
