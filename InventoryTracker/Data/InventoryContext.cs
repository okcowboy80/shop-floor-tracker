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
    public DbSet<RoutingStep> RoutingSteps => Set<RoutingStep>();
    public DbSet<WorkOrder> WorkOrders => Set<WorkOrder>();
    public DbSet<WorkOrderOperation> WorkOrderOperations => Set<WorkOrderOperation>();
    public DbSet<OperationSignOff> OperationSignOffs => Set<OperationSignOff>(); // New

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

        // A part can't have two steps at the same sequence number
        modelBuilder.Entity<RoutingStep>()
            .HasIndex(r => new { r.ItemId, r.Sequence })
            .IsUnique();

        // Don't allow deleting an operation used by a routing
        modelBuilder.Entity<RoutingStep>()
            .HasOne(r => r.Operation)
            .WithMany()
            .HasForeignKey(r => r.OperationId)
            .OnDelete(DeleteBehavior.Restrict);

        // Every job number must be unique
        modelBuilder.Entity<WorkOrder>()
            .HasIndex(w => w.JobNumber)
            .IsUnique();

        // Store status as readable text instead of a number
        modelBuilder.Entity<WorkOrder>()
            .Property(w => w.Status)
            .HasConversion<string>();

        // Don't allow deleting a part that has work orders
        modelBuilder.Entity<WorkOrder>()
            .HasOne(w => w.Item)
            .WithMany()
            .HasForeignKey(w => w.ItemId)
            .OnDelete(DeleteBehavior.Restrict);

        // A job can't have two operations at the same sequence number
        modelBuilder.Entity<WorkOrderOperation>()
            .HasIndex(o => new { o.WorkOrderId, o.Sequence })
            .IsUnique();

        // Store operation status as readable text
        modelBuilder.Entity<WorkOrderOperation>()
            .Property(o => o.Status)
            .HasConversion<string>();

        // Deleting a work order must not erase its operation history
        modelBuilder.Entity<WorkOrderOperation>()
            .HasOne(o => o.WorkOrder)
            .WithMany(w => w.Operations)
            .HasForeignKey(o => o.WorkOrderId)
            .OnDelete(DeleteBehavior.Restrict);

        // Don't allow deleting an operation used on a job
        modelBuilder.Entity<WorkOrderOperation>()
            .HasOne(o => o.Operation)
            .WithMany()
            .HasForeignKey(o => o.OperationId)
            .OnDelete(DeleteBehavior.Restrict);

        // New: store sign-off type as readable text
        modelBuilder.Entity<OperationSignOff>()
            .Property(s => s.Type)
            .HasConversion<string>();

        // New: deleting an operation row must not erase its sign-offs
        modelBuilder.Entity<OperationSignOff>()
            .HasOne(s => s.WorkOrderOperation)
            .WithMany(o => o.SignOffs)
            .HasForeignKey(s => s.WorkOrderOperationId)
            .OnDelete(DeleteBehavior.Restrict);

        // New: don't allow deleting an operator with sign-off history
        modelBuilder.Entity<OperationSignOff>()
            .HasOne(s => s.Operator)
            .WithMany()
            .HasForeignKey(s => s.OperatorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}