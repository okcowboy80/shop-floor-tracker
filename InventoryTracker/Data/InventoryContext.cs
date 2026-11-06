using System.IO;
using InventoryTracker.Models;
using Microsoft.EntityFrameworkCore;

namespace InventoryTracker.Data;

public class InventoryContext : DbContext
{
    public DbSet<Item> Items => Set<Item>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<StockLevel> StockLevels => Set<StockLevel>();

    public DbSet<Operation> Operations => Set<Operation>();
    public DbSet<Operator> Operators => Set<Operator>();

    public static string DbPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "InventoryTracker", "inventory.db");

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(DbPath)!);
        options.UseSqlite($"Data Source={DbPath}");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Item>()
            .HasIndex(i => i.PartNumber)
            .IsUnique();

        modelBuilder.Entity<Location>()
            .HasIndex(l => l.Name)
            .IsUnique();

        // An item can only appear once per location
        modelBuilder.Entity<StockLevel>()
            .HasIndex(s => new { s.ItemId, s.LocationId })
            .IsUnique();

        // Don't allow deleting a location that still has stock in it
        modelBuilder.Entity<StockLevel>()
            .HasOne(s => s.Location)
            .WithMany(l => l.StockLevels)
            .HasForeignKey(s => s.LocationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}