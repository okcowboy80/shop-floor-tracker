using System.IO;
using InventoryTracker.Models;
using Microsoft.EntityFrameworkCore;

namespace InventoryTracker.Data;

public class InventoryContext : DbContext
{
    public DbSet<Item> Items => Set<Item>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<StockLevel> StockLevels => Set<StockLevel>();

    // Shop floor tables
    public DbSet<Operator> Operators => Set<Operator>();
    public DbSet<Operation> Operations => Set<Operation>();
    public DbSet<RoutingStep> RoutingSteps => Set<RoutingStep>(); // New

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

        // No two operators can share a badge number
        modelBuilder.Entity<Operator>()
            .HasIndex(o => o.BadgeNumber)
            .IsUnique();

        // No duplicate operation names
        modelBuilder.Entity<Operation>()
            .HasIndex(o => o.Name)
            .IsUnique();

        // New: a part can't have two steps at the same sequence number
        modelBuilder.Entity<RoutingStep>()
            .HasIndex(r => new { r.ItemId, r.Sequence })
            .IsUnique();

        // New: don't allow deleting an operation used by a routing
        modelBuilder.Entity<RoutingStep>()
            .HasOne(r => r.Operation)
            .WithMany()
            .HasForeignKey(r => r.OperationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}