namespace Yodaphone.Web.Chat.Agents.Copilot;

/// <summary>
/// Server-side credentials and request settings used by the GitHub Copilot runtime.
/// </summary>
/// <remarks>
/// Do not populate this value from a browser request. Configure it with an environment
/// variable or a development secret so the credential remains on the web server.
/// </remarks>
public sealed class CopilotOptions
{
    public const string SectionName = "Chat:Copilot";

    /// <summary>
    /// A GitHub token for an account that is entitled to use GitHub Copilot.
    /// </summary>
    public string? GitHubToken { get; init; }

    /// <summary>
    /// Maximum time for a complete Copilot request before switching to offline replies.
    /// </summary>
    public int ResponseTimeoutSeconds { get; init; } = 30;
}
