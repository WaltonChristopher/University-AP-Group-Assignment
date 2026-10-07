namespace Yodaphone.Web.Domain;

/// <summary>
/// Screens chat message content for safety and security issues before it is
/// stored or sent to an AI agent. Covers prompt-injection style role spoofing
/// and common injection-attack patterns only — general language/profanity
/// filtering is tracked separately.
/// </summary>
public static class ChatContentSanitizer
{
    private static readonly string[] RoleSpoofPrefixes =
    [
        "system:",
        "assistant:",
        "developer:",
        "tool:"
    ];

    // Defense-in-depth only: EF Core parameterises queries by default, so this
    // is not the primary SQL-injection defence. It exists so obviously malicious
    // content never gets stored or forwarded to the AI agent in the first place.
    private static readonly string[] InjectionPatterns =
    [
        "drop table",
        "union select",
        "xp_cmdshell",
        "--",
        "' or '1'='1",
        "\" or \"1\"=\"1"
    ];

    private static readonly string[] ForbiddenAgentResponsePatterns =
    [
        "password",
        "credit card",
        "card number",
        "cvv",
        "social security",
        "system prompt"
    ];

    public static void EnsureSafeUserContent(string content)
    {
        var normalised = content.Trim().ToLowerInvariant();

        foreach (var prefix in RoleSpoofPrefixes)
        {
            if (normalised.StartsWith(prefix, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Message content cannot begin with a role label.",
                    nameof(content));
            }
        }

        foreach (var pattern in InjectionPatterns)
        {
            if (normalised.Contains(pattern, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Message content contains characters or phrases that are not allowed.",
                    nameof(content));
            }
        }
    }

    public static void EnsureSafeAgentResponse(string content)
    {
        var normalised = content.ToLowerInvariant();

        foreach (var pattern in ForbiddenAgentResponsePatterns)
        {
            if (normalised.Contains(pattern, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "The agent response contained content that is not allowed.");
            }
        }
    }
}