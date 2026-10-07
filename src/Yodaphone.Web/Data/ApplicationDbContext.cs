using Microsoft.EntityFrameworkCore;
using Yodaphone.Web.Data.Entities;

namespace Yodaphone.Web.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();

    public DbSet<Conversation> Conversations => Set<Conversation>();

    public DbSet<Message> Messages => Set<Message>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>()
            .HasKey(user => user.UserId);

        modelBuilder.Entity<Conversation>()
            .HasKey(conversation => conversation.ConversationId);

        modelBuilder.Entity<Message>()
            .HasKey(message => message.MessageId);

        modelBuilder.Entity<Conversation>()
            .HasIndex(c => c.Id)
            .IsUnique();

        modelBuilder.Entity<Message>()
            .HasIndex(m => m.Id)
            .IsUnique();

        modelBuilder.Entity<Conversation>()
            .HasOne(c => c.User)
            .WithMany(u => u.Conversations)
            .HasForeignKey(c => c.UserId);

        modelBuilder.Entity<Message>()
            .HasOne(m => m.Sender)
            .WithMany(u => u.Messages)
            .HasForeignKey(m => m.SenderId);

        modelBuilder.Entity<Message>()
            .HasOne(m => m.Conversation)
            .WithMany(c => c.Messages)
            .HasForeignKey(m => m.ConversationId);
    }
}
