namespace Yodaphone.Web.Models;

/// <summary>
/// The root conversation object. Maps to the "Conversation: Dict/Database object"
/// entity in the schema diagram, with AllMessages as an ordered collection of
/// Message objects (a List is used here in place of the sketched Dict, keyed
/// implicitly by insertion order / MessageId).
/// </summary>
public class Conversation
{
    public DateTime DateTime { get; set; } = DateTime.Now;

    public List<Message> AllMessages { get; set; } = new();

    public string AiKey { get; set; } = string.Empty;

    public int TokensUsed { get; set; }

    public void AddMessage(Message message)
    {
        AllMessages.Add(message);
        DateTime = DateTime.Now;
    }
}
