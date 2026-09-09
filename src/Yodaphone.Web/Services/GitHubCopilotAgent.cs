using GitHub.Copilot;


/// <summary>
/// An implementation of IChatAgent that integrates with the GitHub Copilot service.
/// </summary>
public class GitHubCopilotAgent : IChatAgent
{

    private const string SystemPrompt = """
        You are Yodaphone, a helpful customer-service assistant.
        Be concise, polite, and clear.
        Do not request passwords, payment-card details, or other sensitive data.
        If you cannot safely help, explain that a human support colleague can assist.
    """;

    /// <summary>
    /// Gets a reply from the GitHub Copilot service based on the chat history.
    /// </summary>
    /// <param name="history">The chat history.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The agent's response.</returns>
    public async Task<AgentChatResponse> GetReplyAsync(
        IReadOnlyList<ChatMessage> history,
        CancellationToken cancellationToken
    )
    {
        // Error handling for null or empty message list, or cancellation requested
        ArgumentNullException.ThrowIfNull(history, nameof(history));

        if (history.Count == 0)
        {
            throw new ArgumentException("The message list cannot be empty.", nameof(history));
        }

        cancellationToken.ThrowIfCancellationRequested();

        // Initialize the runtime directory for the Copilot client
        var runtimeDirectory = Path.Combine(Path.GetTempPath(), "Yodaphone-Copilot", "temp runtime"); //TODO: replace temp runtime with conversation ID

        // Initialize the Copilot client with the runtime directory
        await using var copilotClient = new CopilotClient(new CopilotClientOptions
        {
            BaseDirectory = runtimeDirectory,
            Mode = CopilotClientMode.Empty // Use the empty mode to avoid unnecessary features
        });

        await copilotClient.StartAsync(cancellationToken);

        // Create a session with the Copilot client
        await using var session = await copilotClient.CreateSessionAsync(new SessionConfig
        {
            ClientName = "Yodaphone-web",
            Model = "auto",
            AvailableTools = [], // No tools available for simplicity
            InfiniteSessions = new InfiniteSessionConfig
            {
                Enabled = false
            },
            SystemMessage = new SystemMessageConfig
            {
                Mode = SystemMessageMode.Append,
                Content = SystemPrompt
            }
        }, cancellationToken);

        // Send the message to the Copilot session and wait for a response
        var response = await session.SendAndWaitAsync(
            new MessageOptions
            {
                Prompt = BuildTranscript(history)
            },
            cancellationToken: cancellationToken
        );

        var content = response?.Data.Content;

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException("The Copilot response was empty or null.");
        }

        return new AgentChatResponse
        {
            Content = content
        };
    }

    /// <summary>
    /// Builds a transcript from the chat history to provide context for the Copilot session.
    /// </summary>
    /// <param name="history"></param>
    /// <returns></returns>
    private static string BuildTranscript(IEnumerable<ChatMessage> history)
    {
        var lines = history.Select(msg => $"{msg.Role}: {msg.Content}");
        return $"{string.Join(Environment.NewLine, lines)}{Environment.NewLine}Yodaphone assistant: ";
    }
}