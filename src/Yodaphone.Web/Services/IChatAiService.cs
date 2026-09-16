using Yodaphone.Web.Models;

namespace Yodaphone.Web.Services;

/// <summary>
/// Abstraction over the YodaPhone AI backend. Swap the DI registration from
/// MockChatAiService to a real HTTP-backed implementation once the API exists —
/// no component code needs to change.
/// </summary>
public interface IChatAiService
{
    Task<Message> SendMessageAsync(string userMessageContent);
}
