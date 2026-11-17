using InventoryTracker.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InventoryTracker.Tests;

public sealed class TestDatabase : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<InventoryContext> _options;

    public TestDatabase()
    {
        // An in-memory SQLite database only exists while this connection is open
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<InventoryContext>()
            .UseSqlite(_connection)
            .Options;

        // Build all tables, indexes, and rules from InventoryContext
        using var db = CreateContext();
        db.Database.EnsureCreated();
    }

    public InventoryContext CreateContext() => new InventoryContext(_options);

    // Closing the connection throws the whole database away
    public void Dispose() => _connection.Dispose();
}