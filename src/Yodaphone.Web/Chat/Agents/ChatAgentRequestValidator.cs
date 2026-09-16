using Yodaphone.Web.Domain;

namespace Yodaphone.Web.Chat.Agents;

internal static class ChatAgentRequestValidator
{
    public static void Validate(
        IReadOnlyList<ChatMessage> history,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(history);

        if (history.Count == 0)
        {
            throw new ArgumentException("The message list cannot be empty.", nameof(history));
        }

        cancellationToken.ThrowIfCancellationRequested();
    }
}
