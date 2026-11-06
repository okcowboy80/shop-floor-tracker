
namespace InventoryTracker.Models
{
    public class OperationSignOff
    {
        public int Id { get; set; }

        public int WorkOrderOperationId { get; set; }   // foreign key to the job's operation
        public WorkOrderOperation WorkOrderOperation { get; set; } = null!;  // navigation property

        public int OperatorId { get; set; } // foreign key to the operator
        public Operator Operator { get; set; } = null!;

        public SignOffType Type { get; set; }

        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

        public string Notes { get; set; } = "";
    }
}
