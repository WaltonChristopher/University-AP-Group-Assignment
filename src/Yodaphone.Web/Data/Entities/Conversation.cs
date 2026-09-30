namespace Yodaphone.Web.Data.Entities;

public class Conversation
{
    public int ConversationId { get; set; }

    public int UserId { get; set; }

    public DateTime Start { get; set; }

    public DateTime DateTime { get; set; }

    public DateTime LastUpdated { get; set; }

    public bool Closed { get; set; }

    public User User { get; set; } = null!;

    public ICollection<Message> Messages { get; set; }
        = new List<Message>();
}