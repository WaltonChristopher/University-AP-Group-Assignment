using Yodaphone.Web.Domain;

namespace Yodaphone.Domain.Tests;

public sealed class MessageRestorationTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t\n")]
    public void Restore_RejectsEmptyContent(string? content)
    {
        var exception = Assert.Throws<ArgumentException>(() => ChatMessage.Restore(
            Guid.NewGuid(), Guid.NewGuid(), MessageRole.Assistant, content!, DateTimeOffset.UtcNow));
        Assert.Equal("content", exception.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void AddAssistantMessage_RejectsEmptyContentWithoutChangingConversation(string? content)
    {
        var conversation = new Conversation(Guid.NewGuid(), DateTimeOffset.UtcNow);
        var activity = conversation.LastActivityAt;
        Assert.Throws<ArgumentException>(() => conversation.AddMessage(MessageRole.Assistant, content!));
        Assert.Empty(conversation.Messages);
        Assert.Equal(activity, conversation.LastActivityAt);
    }

    [Theory]
    [InlineData(MessageRole.Assistant, true)]
    [InlineData(MessageRole.Customer, false)]
    [InlineData(MessageRole.System, false)]
    public void Restore_PreservesIdentityTimestampAndAgentRole(MessageRole role, bool isAgent)
    {
        var id = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        var timestamp = DateTimeOffset.Parse("2026-10-01T12:34:00Z");
        var message = ChatMessage.Restore(id, conversationId, role, "Saved text", timestamp);
        Assert.Equal(id, message.Id);
        Assert.Equal(conversationId, message.ConversationId);
        Assert.Equal(timestamp, message.SentAt);
        Assert.Equal(role, message.Role);
        Assert.Equal("Saved text", message.Content);
        Assert.Equal(isAgent, message.IsFromAgent);
    }

    [Fact]
    public void RestoreConversation_RejectsNullMessages() =>
        Assert.Throws<ArgumentNullException>(() => Conversation.Restore(Guid.NewGuid(),
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, ConversationStatus.Active, null!));

    [Fact]
    public void RestoreConversation_RejectsMessagesFromAnotherConversation()
    {
        var message = ChatMessage.Restore(Guid.NewGuid(), Guid.NewGuid(), MessageRole.Customer,
            "Other chat", DateTimeOffset.UtcNow);
        Assert.Throws<ArgumentException>(() => Conversation.Restore(Guid.NewGuid(),
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, ConversationStatus.Active, [message]));
    }

    [Fact]
    public void RestoreConversation_SortsMessagesAndPreservesClosedState()
    {
        var id = Guid.NewGuid();
        var start = DateTimeOffset.Parse("2026-10-01T12:00:00Z");
        var first = ChatMessage.Restore(Guid.NewGuid(), id, MessageRole.Customer, "Question", start);
        var second = ChatMessage.Restore(Guid.NewGuid(), id, MessageRole.Assistant, "Answer", start.AddMinutes(1));
        var conversation = Conversation.Restore(id, start, second.SentAt, ConversationStatus.Closed, [second, first]);
        Assert.Equal(new[] { first, second }, conversation.Messages);
        Assert.Equal(id, conversation.Id);
        Assert.Equal(start, conversation.StartedAt);
        Assert.Equal(second.SentAt, conversation.LastActivityAt);
        Assert.Equal(ConversationStatus.Closed, conversation.Status);
        Assert.Throws<InvalidOperationException>(() => conversation.AddMessage(MessageRole.Customer, "More"));
    }
}
