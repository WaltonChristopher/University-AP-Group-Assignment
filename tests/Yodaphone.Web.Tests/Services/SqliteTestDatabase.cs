using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Yodaphone.Web.Data;

namespace Yodaphone.Web.Tests.Services;

/// <summary>
/// Provides an isolated SQLite file and fresh contexts for persistence integration tests.
/// </summary>
/// <remarks>
/// Separate connections verify that changes are committed and visible outside
/// the writing context. The temporary file is deleted when the test finishes.
/// </remarks>
internal sealed class SqliteTestDatabase : IDbContextFactory<ApplicationDbContext>, IAsyncDisposable
{
    private readonly string path = Path.Combine(Path.GetTempPath(), $"yodaphone-tests-{Guid.NewGuid():N}.db");
    private readonly DbContextOptions<ApplicationDbContext> options;
    private readonly List<ApplicationDbContext> contexts = [];

    private SqliteTestDatabase()
    {
        options = new DbContextOptionsBuilder<ApplicationDbContext>()
            // Avoid pooled connections retaining a Windows file handle during cleanup.
            .UseSqlite(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString())
            .Options;
    }

    /// <summary>
    /// Creates a temporary database using the application's real schema migrations.
    /// </summary>
    public static async Task<SqliteTestDatabase> CreateAsync()
    {
        var database = new SqliteTestDatabase();
        try
        {
            await using var context = database.CreateDbContext();
            await context.Database.MigrateAsync();
            return database;
        }
        catch
        {
            await database.DisposeAsync();
            throw;
        }
    }

    /// <summary>
    /// Creates a context and records it so tests can check that callers dispose it.
    /// </summary>
    public ApplicationDbContext CreateDbContext()
    {
        var context = new ApplicationDbContext(options);
        contexts.Add(context);
        return context;
    }

    /// <inheritdoc />
    public Task<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(CreateDbContext());
    }

    /// <summary>
    /// Checks that no database context remains usable after an operation completes.
    /// </summary>
    public void AssertAllContextsDisposed()
    {
        Assert.NotEmpty(contexts);
        foreach (var context in contexts)
        {
            Assert.Throws<ObjectDisposedException>(() => context.ChangeTracker.Entries().ToArray());
        }
    }

    /// <summary>
    /// Deletes the temporary database after callers have disposed their contexts.
    /// </summary>
    public ValueTask DisposeAsync()
    {
        File.Delete(path);
        return ValueTask.CompletedTask;
    }
}
