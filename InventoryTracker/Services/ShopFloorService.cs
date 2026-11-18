// ============================================================================
// ShopFloorService
// ----------------------------------------------------------------------------
// Holds the shop floor rules for the traveler: what has to be true before a
// job can move forward, and what changes when it does.
//
// Right now it has one action, ReleaseWorkOrder. Releasing a job:
//   1. Checks the job exists, is still a Draft, and has a quantity.
//   2. Looks up the part's routing (Shear/Cut, Punch, Brake, and so on).
//   3. Copies each routing step onto the job as a WorkOrderOperation. This is
//      the "snapshot," so later routing changes can't rewrite this job.
//   4. Marks the job Released with a timestamp.
//   5. Saves everything at once, so the job is never half-released.
//
// If a rule fails, it throws an InvalidOperationException with a message that
// the screen can show to the user later.
// ============================================================================

using InventoryTracker.Data;   // InventoryContext
using InventoryTracker.Models; // WorkOrder, RoutingStep, WorkOrderOperation, statuses

namespace InventoryTracker.Services;

public class ShopFloorService
{
    // The database this service reads from and writes to.
    // "readonly" means it can only be set in the constructor.
    private readonly InventoryContext _db;

    // The caller passes the database in (dependency injection).
    // The app will pass the real inventory.db; tests pass the in-memory one.
    public ShopFloorService(InventoryContext db)
    {
        _db = db;
    }

    public void ReleaseWorkOrder(int workOrderId)
    {
        // Find the job by its Id. SingleOrDefault returns null if there's no match,
        // and "?? throw" turns that null into an error with a clear message.
        var workOrder = _db.WorkOrders.SingleOrDefault(w => w.Id == workOrderId)
            ?? throw new InvalidOperationException($"Work order {workOrderId} was not found.");

        // Rule: only a Draft job can be released.
        // This also stops a job from being released twice and getting duplicate operations.
        if (workOrder.Status != WorkOrderStatus.Draft)
        {
            throw new InvalidOperationException(
                $"Job {workOrder.JobNumber} is {workOrder.Status} and can't be released.");
        }

        // Rule: a job has to be for at least one part.
        if (workOrder.Quantity <= 0)
        {
            throw new InvalidOperationException(
                $"Job {workOrder.JobNumber} must have a quantity greater than zero.");
        }

        // Get this part's routing steps, in order.
        // EF Core turns this LINQ into:
        //   SELECT * FROM RoutingSteps WHERE ItemId = @itemId ORDER BY Sequence
        // ToList() runs the query and brings the rows into memory.
        var routing = _db.RoutingSteps
            .Where(r => r.ItemId == workOrder.ItemId)
            .OrderBy(r => r.Sequence)
            .ToList();

        // Rule: a job can't go to the floor without a routing.
        if (routing.Count == 0)
        {
            throw new InvalidOperationException(
                $"Job {workOrder.JobNumber} can't be released because its part has no routing.");
        }

        // Snapshot the routing onto this job's traveler.
        // Each routing step becomes a WorkOrderOperation that belongs to this job.
        // Status isn't set here because it already defaults to Pending.
        foreach (var step in routing)
        {
            workOrder.Operations.Add(new WorkOrderOperation
            {
                OperationId = step.OperationId,
                Sequence = step.Sequence
            });
        }

        // Mark the job as released, and record when (in UTC).
        workOrder.Status = WorkOrderStatus.Released;
        workOrder.ReleasedAtUtc = DateTime.UtcNow;

        // Save everything in one database transaction:
        // the new operations and the status change are saved together, or not at all.
        _db.SaveChanges();
    }
}