namespace InventoryTracker.Models;

public class Operator
{
    public int Id { get; set; }
    public string BadgeNumber { get; set; } = "";
    public string Name { get; set; } = "";
    public bool IsActive { get; set; } = true;
}