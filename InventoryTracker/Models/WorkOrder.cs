
namespace InventoryTracker.Models
{
    public class WorkOrder
    {
        public int Id { get; set; }

        public string JobNumber { get; set; } = "";

        public int ItemId { get; set; }          // foreign key to the part
        public Item Item { get; set; } = null!;  // navigation property

        public int Quantity { get; set; }

        public WorkOrderStatus Status { get; set; } = WorkOrderStatus.Draft;

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public DateTime? ReleasedAtUtc { get; set; }
        public DateTime? CompletedAtUtc { get; set; }

    }
}
