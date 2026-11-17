using InventoryTracker.Models;
using Microsoft.EntityFrameworkCore;

namespace InventoryTracker.Tests;

public class DatabaseSetupTests
{
    [Fact]
    public void Operator_CanBeSavedAndReadBack()
    {
        // Arrange: create a fresh database
        using var testDb = new TestDatabase();

        // Act: save an operator
        using (var db = testDb.CreateContext())
        {
            db.Operators.Add(new Operator { BadgeNumber = "1042", Name = "Jeremy Hill" });
            db.SaveChanges();
        }

        // Assert: read it back with a new context
        using (var db = testDb.CreateContext())
        {
            var op = Assert.Single(db.Operators);
            Assert.Equal("1042", op.BadgeNumber);
            Assert.Equal("Jeremy Hill", op.Name);
            Assert.True(op.IsActive);
        }
    }

    [Fact]
    public void Operator_DuplicateBadgeNumber_IsRejected()
    {
        // Arrange: two operators with the same badge number
        using var testDb = new TestDatabase();
        using var db = testDb.CreateContext();

        db.Operators.Add(new Operator { BadgeNumber = "1042", Name = "Jeremy Hill" });
        db.Operators.Add(new Operator { BadgeNumber = "1042", Name = "Claude" });

        // Act and Assert: saving must fail because of the unique index
        Assert.Throws<DbUpdateException>(() => db.SaveChanges());
    }
}