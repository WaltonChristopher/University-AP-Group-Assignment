using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Yodaphone.Web.Data;

namespace Yodaphone.Web.Tests.Integration;

public sealed class ChatApplicationIntegrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetChatPage_migrates_database_and_restores_the_same_welcome_conversation()
    {
        await using var factory = new ChatApplicationFactory();
        using var client = factory.CreateClient();

        using var firstResponse = await client.GetAsync("/");
        var firstHtml = await firstResponse.Content.ReadAsStringAsync();

        firstResponse.EnsureSuccessStatusCode();
        Assert.Contains("How can I help with your account today?", firstHtml);

        using var secondResponse = await client.GetAsync("/");
        var secondHtml = await secondResponse.Content.ReadAsStringAsync();

        secondResponse.EnsureSuccessStatusCode();
        Assert.Contains("How can I help with your account today?", secondHtml);

        var dbFactory = factory.Services.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
        await using var database = await dbFactory.CreateDbContextAsync();
        var conversation = await database.Conversations
            .Include(item => item.Messages)
            .SingleAsync();

        Assert.Equal(42, conversation.UserId);
        Assert.Equal("Hello! I'm YodaPhone AI. How can I help with your account today?",
            Assert.Single(conversation.Messages).MessageContent);
    }
}
