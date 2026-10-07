using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;
using Yodaphone.Web.Domain;
using Yodaphone.Web.Services;

namespace Yodaphone.Web.Tests.Services;

public sealed class ConversationMigrationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MigrateAsync_keeps_legacy_chats_accessible_and_message_ids_stable(bool identifiersAlreadyBackfilled)
    {
        await using var database = await SqliteTestDatabase.CreateAsync("20261007085950_InitialCreate");
        var timestamp = new DateTime(2026, 10, 7, 10, 0, 0, DateTimeKind.Utc);
        await using (var context = database.CreateDbContext())
        {
            await context.Database.ExecuteSqlRawAsync("INSERT INTO Users (UserId, Support) VALUES (42, 0);");
            for (var id = 1; id <= 2; id++)
            {
                await context.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO Conversations (ConversationId, UserId, Start, DateTime, LastUpdated, Closed)
                    VALUES ({id}, 42, {timestamp}, {timestamp}, {timestamp}, 0);
                    """);
                await context.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO Messages (MessageContent, DateTime, Status, SenderId, ConversationId)
                    VALUES ({$"Legacy message {id}"}, {timestamp}, 'Customer', 42, {id});
                    """);
            }
            await context.GetService<IMigrator>().MigrateAsync("20261007120903_AddConversationDomainIdentifiers");
            if (identifiersAlreadyBackfilled)
            {
                // Reproduce databases that already ran the original lowercase backfill.
                await context.Database.ExecuteSqlRawAsync("UPDATE Conversations SET Id = lower(Id);");
                await context.Database.ExecuteSqlRawAsync("UPDATE Messages SET Id = lower(Id);");
            }
        }

        var repository = new SqliteConversationRepository(database);
        var before = await repository.GetAllAsync(42);
        Assert.Equal(2, before.Count);
        Assert.Equal(2, before.Select(chat => chat.Id).Distinct().Count());
        Assert.Equal(2, before.SelectMany(chat => chat.Messages).Select(message => message.Id).Distinct().Count());
        if (identifiersAlreadyBackfilled)
        {
            await using var context = database.CreateDbContext();
            await context.Database.MigrateAsync();
        }

        foreach (var saved in before)
        {
            Assert.NotEqual(Guid.Empty, saved.Id);
            var restored = await repository.GetAsync(saved.Id, 42);
            Assert.NotNull(restored);
            Assert.Equal(saved.StartedAt, restored.StartedAt);
            Assert.Equal(saved.LastActivityAt, restored.LastActivityAt);
            var message = Assert.Single(restored.Messages);
            Assert.Equal(Assert.Single(saved.Messages).Id, message.Id);
            Assert.Equal(Assert.Single(saved.Messages).Content, message.Content);
            restored.AddMessage(MessageRole.Assistant, "Saved after upgrade");
            await repository.SaveAsync(restored, 42);
            await repository.SaveAsync(restored, 42);
            var afterSave = await repository.GetAsync(saved.Id, 42);
            Assert.NotNull(afterSave);
            Assert.Equal(new[] { message.Id, restored.Messages.Last().Id }, afterSave.Messages.Select(item => item.Id));
        }
        database.AssertAllContextsDisposed();
    }
}
