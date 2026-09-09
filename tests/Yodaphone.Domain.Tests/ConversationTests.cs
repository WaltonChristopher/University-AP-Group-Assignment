using Yodaphone.Web.Domain;

namespace Yodaphone.Domain.Tests;

public sealed class ConversationTests
{
    [Fact]
    public void AddMessage_appends_message_and_updates_last_activity()
    {
        var conversation = new Conversation(Guid.NewGuid(), DateTimeOffset.UtcNow);

        var message = conversation.AddMessage(MessageRole.Customer, "Hello");

        Assert.Single(conversation.Messages);
        Assert.Equal(message, conversation.Messages.First());
    }

    [Fact]
    public void AddMessage_after_close_throws()
    {
        var conversation = new Conversation(Guid.NewGuid(), DateTimeOffset.UtcNow);
        conversation.Close();

        Assert.Throws<InvalidOperationException>(
            () => conversation.AddMessage(MessageRole.Customer, "Hello"));
    }
}