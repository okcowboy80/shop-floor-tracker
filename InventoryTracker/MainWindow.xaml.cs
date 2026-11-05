using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using InventoryTracker.Data;
using InventoryTracker.Models;
using Microsoft.EntityFrameworkCore;

namespace InventoryTracker;

public partial class MainWindow : Window
{
    private readonly InventoryContext _db = new();

    public MainWindow()
    {
        InitializeComponent();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        _db.Database.Migrate();
        LoadData();
    }

    private void LoadData()
    {
        _db.Locations.Load();
        _db.Items.Include(i => i.StockLevels).Load();   // loads items AND their stock rows

        var locations = _db.Locations.Local.ToObservableCollection();
        LocationColumn.ItemsSource = locations;
        LocationsGrid.ItemsSource = locations;
        ItemsGrid.ItemsSource = _db.Items.Local.ToObservableCollection();
    }

    private void LocationsGrid_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Delete)
            return;

        // If you're editing a cell, let Delete erase text like normal
        if (e.OriginalSource is TextBox)
            return;

        var locationsWithStock = LocationsGrid.SelectedItems
            .OfType<Location>()
            .Where(l => l.StockLevels.Any())
            .Select(l => l.Name)
            .ToList();

        if (locationsWithStock.Count > 0)
        {
            MessageBox.Show(
                "These locations still have stock and can't be deleted:\n\n" +
                string.Join("\n", locationsWithStock) +
                "\n\nMove or remove the stock first.",
                "Can't delete location", MessageBoxButton.OK, MessageBoxImage.Warning);

            e.Handled = true;   // cancels the delete
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        ItemsGrid.CommitEdit(DataGridEditingUnit.Row, true);
        StockGrid.CommitEdit(DataGridEditingUnit.Row, true);
        LocationsGrid.CommitEdit(DataGridEditingUnit.Row, true);

        try
        {
            _db.SaveChanges();
            MessageBox.Show("Saved.");
        }
        catch (Exception ex) when (ex is DbUpdateException or InvalidOperationException)
        {
            var message = ex.InnerException?.Message ?? ex.Message;
            var result = MessageBox.Show(
                $"Could not save:\n{message}\n\nDiscard unsaved changes and reload from the database?",
                "Save failed", MessageBoxButton.YesNo, MessageBoxImage.Error);

            if (result == MessageBoxResult.Yes)
            {
                _db.ChangeTracker.Clear();   // forget all pending changes
                LoadData();
            }
        }
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        _db.Dispose();
    }
}