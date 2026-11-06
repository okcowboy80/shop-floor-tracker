namespace InventoryTracker.Models;

public class Operation
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public bool IsInspection { get; set; }
}