// ============================================================================
// ShopFloorServiceTests
// ----------------------------------------------------------------------------
// Automated tests for ShopFloorService. Each test:
//   - Creates a brand-new in-memory database (TestDatabase), so tests never
//     share data or touch the real inventory.db.
//   - Uses SeedDraftJob to add a part, its routing, and a Draft job.
//   - Runs a service method, then checks the result with a NEW context so the
//     values come from the database, not from EF Core's memory.
//
// Current tests:
//   - ReleaseWorkOrder_CopiesRoutingAndMarksReleased: releasing a job copies
//     the part's routing onto the job in order and marks the job Released.
//   - ReleaseWorkOrder_PartWithNoRouting_IsRejected: a job whose part has no
//     routing can't be released, and the failed attempt leaves the job
//     unchanged (still Draft, with no operations).
// ============================================================================

using InventoryTracker.Data;            // InventoryContext
using InventoryTracker.Models;          // Item, Operation, RoutingStep, WorkOrder, statuses
using InventoryTracker.Services;        // ShopFloorService (the code being tested)
using Microsoft.EntityFrameworkCore;    // Include / ThenInclude

namespace InventoryTracker.Tests;

public class ShopFloorServiceTests
{
    // The routing for the sample bracket part, in shop order.
    // Tests use this list both to build the routing and to check the result.
    private static readonly string[] BracketRouting =
        { "Shear/Cut", "Punch", "Brake", "Fit-up", "Weld/Cleanup", "Inspect" };

    // Test helper: adds a part, an optional routing, and a Draft job, then returns the job's Id.
    // withRouting = false lets a test create a part with no routing.
    private static int SeedDraftJob(InventoryContext db, bool withRouting = true)
    {
        // The part being made. EF Core assigns its Id when it saves.
        var part = new Item { PartNumber = "BRKT-100", Description = "Formed bracket" };
        db.Items.Add(part);

        if (withRouting)
        {
            // Build one routing step per operation: 10, 20, 30, and so on.
            var sequence = 10;
            foreach (var name in BracketRouting)
            {
                // Only the Inspect operation is marked as an inspection step.
                var operation = new Operation { Name = name, IsInspection = name == "Inspect" };

                // Setting Item and Operation (navigation properties) is enough.
                // EF Core saves the new Operation too, then fills in ItemId and OperationId.
                db.RoutingSteps.Add(new RoutingStep { Item = part, Operation = operation, Sequence = sequence });
                sequence += 10;
            }
        }

        // A Draft job for 4 of this part. Status defaults to Draft.
        var job = new WorkOrder { JobNumber = "J-1001", Item = part, Quantity = 4 };
        db.WorkOrders.Add(job);

        // Write the part, operations, routing, and job to the database.
        db.SaveChanges();

        // After saving, job.Id holds the Id the database assigned.
        return job.Id;
    }

    [Fact]
    public void ReleaseWorkOrder_CopiesRoutingAndMarksReleased()
    {
        // Arrange: a fresh database with a Draft job whose part has a routing.
        using var testDb = new TestDatabase();
        int jobId;
        using (var db = testDb.CreateContext())
        {
            jobId = SeedDraftJob(db);
        }

        // Act: release the job, using its own context like the app would.
        using (var db = testDb.CreateContext())
        {
            new ShopFloorService(db).ReleaseWorkOrder(jobId);
        }

        // Assert: read the job back from the database with a new context.
        using (var db = testDb.CreateContext())
        {
            // Include loads the job's operations, and ThenInclude loads each
            // operation's name. Without these, the lists would come back empty.
            var job = db.WorkOrders
                .Include(w => w.Operations)
                .ThenInclude(o => o.Operation)
                .Single(w => w.Id == jobId);

            // The job is now Released, with a release time recorded.
            Assert.Equal(WorkOrderStatus.Released, job.Status);
            Assert.NotNull(job.ReleasedAtUtc);

            // The job's operations match the routing, in the same order.
            var steps = job.Operations.OrderBy(o => o.Sequence).ToList();
            Assert.Equal(BracketRouting, steps.Select(s => s.Operation.Name));

            // Every operation starts out Pending.
            Assert.All(steps, s => Assert.Equal(OperationStatus.Pending, s.Status));
        }
    }

    [Fact]
    public void ReleaseWorkOrder_PartWithNoRouting_IsRejected()
    {
        // Arrange: a fresh database with a Draft job whose part has no routing.
        using var testDb = new TestDatabase();
        int jobId;
        using (var db = testDb.CreateContext())
        {
            jobId = SeedDraftJob(db, withRouting: false);
        }

        // Act and Assert: releasing must throw, and the message must explain why.
        // Assert.Throws returns the exception it caught, so its message can be checked.
        using (var db = testDb.CreateContext())
        {
            var ex = Assert.Throws<InvalidOperationException>(
                () => new ShopFloorService(db).ReleaseWorkOrder(jobId));
            Assert.Contains("no routing", ex.Message);
        }

        // Assert: read the job back with a new context to prove the failed release changed nothing.
        using (var db = testDb.CreateContext())
        {
            // Include loads the job's operations so we can check that none were added.
            var job = db.WorkOrders
                .Include(w => w.Operations)
                .Single(w => w.Id == jobId);

            // Still a Draft, with no release time recorded.
            Assert.Equal(WorkOrderStatus.Draft, job.Status);
            Assert.Null(job.ReleasedAtUtc);

            // No operations were copied onto the job.
            Assert.Empty(job.Operations);
        }
    }
}