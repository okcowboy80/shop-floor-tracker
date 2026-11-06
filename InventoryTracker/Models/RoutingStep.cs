
namespace InventoryTracker.Models
{
    public class RoutingStep
    {
        public int Id { get; set; }

        public int ItemId { get; set; }          // foreign key to the part
        public Item Item { get; set; } = null!;  // navigation property

        public int OperationId { get; set; } // foreign key to the operation
        public Operation Operation { get; set; } = null!;

        public int Sequence { get; set; }
    }
}
