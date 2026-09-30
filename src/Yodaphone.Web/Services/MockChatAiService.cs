using Yodaphone.Web.Domain;
using Yodaphone.Web.Models;

namespace Yodaphone.Web.Services;

/// <summary>
/// Stand-in for the real YodaPhone AI backend. Simulates latency with a
/// Task.Delay and returns a canned, schema-correct Message. Replace this
/// with an HttpClient-based implementation when the real API is ready.
/// </summary>
public class MockChatAiService : IChatService
{
    private static readonly string[] CannedResponses =
    {
        "Thank you for your inquiry. A YodaPhone specialist will be with you shortly.",
        "I understand. Let me look into your account and get back to you.",
        "That is a great question. Here is what I found for you.",
        "Your request has been logged. Reference number attached to this conversation.",
        "I can help with that. Could you tell me a little more about the issue?"
    };

    public Task<ChatMessage> AddMessageAsync(string conversationId, MessageRole role, string content, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task CloseConversationAsync(string conversationId, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<Domain.Conversation> GetConversationAsync(string conversationId, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public async Task<Message> SendMessageAsync(string userMessageContent)
    {
        // Simulate API / network latency.
        await Task.Delay(1500);

        var response = CannedResponses[Random.Shared.Next(CannedResponses.Length)];

        return new Message
        {
            Content = response,
            MessageId = Random.Shared.Next(1000, 9999),
            DateTime = DateTime.Now,
            Status = "delivered",
            Sender = "yoda_ai_bot_01"
        };
    }

    public Task<Domain.Conversation> StartConversationAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
