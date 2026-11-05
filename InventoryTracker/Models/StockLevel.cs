namespace InventoryTracker.Models;

public class StockLevel
{
    public int Id { get; set; }

    public int ItemId { get; set; }          // foreign key
    public Item Item { get; set; } = null!;  // navigation property

    public int LocationId { get; set; }
    public Location Location { get; set; } = null!;

    public int Quantity { get; set; }
}