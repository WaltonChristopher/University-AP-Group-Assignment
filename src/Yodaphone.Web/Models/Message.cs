namespace Yodaphone.Web.Models;

/// <summary>
/// A single chat message exchanged between the user and the YodaPhone AI bot.
/// Maps directly to the "message: Object" entity in the schema diagram.
/// </summary>
public class Message
{
    public string Content { get; set; } = string.Empty;

    public int MessageId { get; set; }

    public DateTime DateTime { get; set; } = DateTime.Now;

    /// <summary>
    /// e.g. "sending" | "sent" | "delivered" | "error"
    /// </summary>
    public string Status { get; set; } = "sent";

    /// <summary>
    /// Identifies who sent the message, e.g. "user" or "yoda_ai_bot_01".
    /// </summary>
    public string Sender { get; set; } = string.Empty;

    /// <summary>
    /// Convenience flag used purely by the UI layer to decide bubble alignment/color.
    /// A message is considered "from the bot" whenever the sender id contains "bot"
    /// or "ai" (case-insensitive) — this keeps the model agnostic of any specific bot id.
    /// </summary>
    public bool IsFromBot =>
        Sender.Contains("bot", StringComparison.OrdinalIgnoreCase) ||
        Sender.Contains("ai", StringComparison.OrdinalIgnoreCase);
}
