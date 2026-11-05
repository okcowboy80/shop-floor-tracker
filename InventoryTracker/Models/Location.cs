using System.Collections.ObjectModel;

namespace InventoryTracker.Models;

public class Location
{
    public int Id { get; set; }
    public string Name { get; set; } = "";

    public ICollection<StockLevel> StockLevels { get; set; } = new ObservableCollection<StockLevel>();
}