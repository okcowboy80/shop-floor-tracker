// ============================================================================
// ShopFloorService
// ----------------------------------------------------------------------------
// Holds the shop floor rules for the traveler: what has to be true before a
// job or an operation can move forward, and what changes when it does.
//
// Actions:
//   ReleaseWorkOrder  - Copies the part's routing onto a Draft job (the
//                       "snapshot") and marks the job Released.
//   StartOperation    - An operator signs off that they started a step.
//                       Every earlier step must already be completed.
//   CompleteOperation - An operator signs off that a started step is done.
//                       When the last step is completed, the job is completed.
//
// Operators sign off by badge number, like scanning a badge at a station.
// Every sign-off is added as a new OperationSignOff row with a UTC timestamp,
// so the history is never overwritten.
//
// Each action saves all of its changes in one SaveChanges call, so it either
// fully happens or doesn't happen at all. If a rule fails, the action throws
// an InvalidOperationException with a message the screen can show later.
// ============================================================================

using InventoryTracker.Data;            // InventoryContext
using InventoryTracker.Models;          // WorkOrder, WorkOrderOperation, Operator, statuses
using Microsoft.EntityFrameworkCore;    // Include / ThenInclude

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

    // ------------------------------------------------------------------------
    // Release a Draft job so it can go to the floor.
    // ------------------------------------------------------------------------
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

    // ------------------------------------------------------------------------
    // An operator starts one step of a job.
    // "notes" is optional; "= """ makes it default to an empty string.
    // ------------------------------------------------------------------------
    public void StartOperation(int workOrderOperationId, string badgeNumber, string notes = "")
    {
        // Load the step with its job and all of the job's other steps,
        // and look up the operator by badge (both helpers are at the bottom).
        var operation = LoadOperation(workOrderOperationId);
        var signer = FindActiveOperator(badgeNumber);
        var job = operation.WorkOrder;

        // Rule: the job has to be on the floor. A Draft job hasn't been released,
        // and an OnHold, Completed, or Cancelled job can't have work started on it.
        if (job.Status != WorkOrderStatus.Released && job.Status != WorkOrderStatus.InProgress)
        {
            throw new InvalidOperationException(
                $"Job {job.JobNumber} is {job.Status}, so its operations can't be started.");
        }

        // Rule: a step can only be started once.
        if (operation.Status != OperationStatus.Pending)
        {
            throw new InvalidOperationException(
                $"{operation.Operation.Name} on job {job.JobNumber} is already {operation.Status}.");
        }

        // Rule: every earlier step must be completed first.
        // Look for the first step with a lower sequence that isn't Completed.
        // FirstOrDefault returns null if there isn't one, which means we're clear.
        var unfinished = job.Operations
            .Where(o => o.Sequence < operation.Sequence && o.Status != OperationStatus.Completed)
            .OrderBy(o => o.Sequence)
            .FirstOrDefault();

        if (unfinished != null)
        {
            throw new InvalidOperationException(
                $"{operation.Operation.Name} can't start until {unfinished.Operation.Name} " +
                $"(step {unfinished.Sequence}) is completed.");
        }

        // Record the sign-off. Setting the Operator navigation property is enough;
        // EF Core fills in OperatorId and WorkOrderOperationId when it saves.
        // TimestampUtc defaults to the current UTC time.
        operation.SignOffs.Add(new OperationSignOff
        {
            Operator = signer,
            Type = SignOffType.Started,
            Notes = notes
        });

        // The step is now being worked, and so is the job.
        operation.Status = OperationStatus.InProgress;
        job.Status = WorkOrderStatus.InProgress;

        // Save the sign-off and both status changes together.
        _db.SaveChanges();
    }

    // ------------------------------------------------------------------------
    // An operator completes a step that has already been started.
    // ------------------------------------------------------------------------
    public void CompleteOperation(int workOrderOperationId, string badgeNumber, string notes = "")
    {
        // Same loading as StartOperation.
        var operation = LoadOperation(workOrderOperationId);
        var signer = FindActiveOperator(badgeNumber);
        var job = operation.WorkOrder;

        // Rule: work can only be completed on a job that's in progress.
        // This blocks completing steps while a job is OnHold.
        if (job.Status != WorkOrderStatus.InProgress)
        {
            throw new InvalidOperationException(
                $"Job {job.JobNumber} is {job.Status}, so its operations can't be completed.");
        }

        // Rule: a step has to be started before it can be completed.
        if (operation.Status != OperationStatus.InProgress)
        {
            throw new InvalidOperationException(
                $"{operation.Operation.Name} on job {job.JobNumber} is {operation.Status} " +
                "and has to be started before it can be completed.");
        }

        // Record the completion sign-off. This can be a different operator
        // than the one who started the step (for example, after a shift change).
        operation.SignOffs.Add(new OperationSignOff
        {
            Operator = signer,
            Type = SignOffType.Completed,
            Notes = notes
        });

        operation.Status = OperationStatus.Completed;

        // If every step on the job is now completed, the job is done.
        // job.Operations includes this same step object, so it already
        // sees the Completed status we just set.
        if (job.Operations.All(o => o.Status == OperationStatus.Completed))
        {
            job.Status = WorkOrderStatus.Completed;
            job.CompletedAtUtc = DateTime.UtcNow;
        }

        // Save the sign-off and the status changes together.
        _db.SaveChanges();
    }

    // ------------------------------------------------------------------------
    // Helpers ("private" means only this class can use them)
    // ------------------------------------------------------------------------

    // Loads one job step, plus everything the rules above need:
    //   - the step's Operation (for its name in error messages)
    //   - its WorkOrder, and all of that job's steps with their Operations
    //     (to check earlier steps and whether the job is finished)
    private WorkOrderOperation LoadOperation(int workOrderOperationId)
    {
        return _db.WorkOrderOperations
            .Include(o => o.Operation)
            .Include(o => o.WorkOrder)
                .ThenInclude(w => w.Operations)
                    .ThenInclude(o => o.Operation)
            .SingleOrDefault(o => o.Id == workOrderOperationId)
            ?? throw new InvalidOperationException(
                $"Job operation {workOrderOperationId} was not found.");
    }

    // Finds the operator with this badge number and makes sure they're active.
    // Inactive operators keep their history but can't sign off new work.
    private Operator FindActiveOperator(string badgeNumber)
    {
        var signer = _db.Operators.SingleOrDefault(o => o.BadgeNumber == badgeNumber)
            ?? throw new InvalidOperationException(
                $"No operator has badge number {badgeNumber}.");

        if (!signer.IsActive)
        {
            throw new InvalidOperationException(
                $"Operator {signer.Name} (badge {badgeNumber}) is inactive and can't sign off work.");
        }

        return signer;
    }
}