namespace Yodaphone.Web.Data.Entities;

public class User
{
    public int UserId { get; set; }

    public bool Support { get; set; }

    public ICollection<Conversation> Conversations { get; set; }
        = new List<Conversation>();

    public ICollection<Message> Messages { get; set; }
        = new List<Message>();
}