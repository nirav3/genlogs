using Genlogs.Api.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Genlogs.Api.Tests.TestSupport;

/// <summary>
/// An isolated, real SQLite database per test (in-memory, one open connection keeps it alive) — exercises
/// the actual EF Core LINQ-to-SQLite translation instead of a fake in-memory provider (design.md: "it
/// doesn't exercise real SQL").
/// </summary>
public sealed class SqliteTestDatabase : IDisposable
{
    private readonly SqliteConnection _connection;

    public GenlogsDbContext Context { get; }

    public SqliteTestDatabase()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<GenlogsDbContext>()
            .UseSqlite(_connection)
            .Options;

        Context = new GenlogsDbContext(options);
        Context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        Context.Dispose();
        _connection.Dispose();
    }
}
