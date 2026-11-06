using System.Collections.ObjectModel;

namespace InventoryTracker.Models
{
    public class WorkOrderOperation
    {
        public int Id { get; set; }

        public int WorkOrderId { get; set; }   // foreign key to the job
        public WorkOrder WorkOrder { get; set; } = null!;  // navigation property

        public int OperationId { get; set; } // foreign key to the operation
        public Operation Operation { get; set; } = null!;

        public int Sequence { get; set; }

        public OperationStatus Status { get; set; } = OperationStatus.Pending;

        public ICollection<OperationSignOff> SignOffs { get; set; } =
            new ObservableCollection<OperationSignOff>();
    }
}