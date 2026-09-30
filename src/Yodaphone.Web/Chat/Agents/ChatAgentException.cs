namespace Yodaphone.Web.Chat.Agents;

/// <summary>
/// Indicates that an external chat provider could not complete a request.
/// Message contains fixed user-facing text. InnerException retains diagnostic details for debugging purposes.
/// </summary>
/// <remarks>
/// Callers should catch this exception and display Message as an error, not save it as an
/// assistant reply. <b>Never display ToString(), the stack trace, or InnerException to users.</b>
/// Handle diagnostic details only through appropriate server-side logging.
/// </remarks>
public sealed class ChatAgentException : Exception
{
    public ChatAgentException(Exception innerException)
        : base("The chat service is temporarily unavailable. Please try again.", innerException)
    {
    }
}
