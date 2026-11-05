using System.Collections.ObjectModel;

namespace InventoryTracker.Models;

public class Item
{
    public int Id { get; set; }
    public string PartNumber { get; set; } = "";
    public string Description { get; set; } = "";
    public string Category { get; set; } = "";
    public decimal UnitCost { get; set; }
    public int ReorderLevel { get; set; }

    public ICollection<StockLevel> StockLevels { get; set; } = new ObservableCollection<StockLevel>();
}