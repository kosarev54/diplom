using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DispatcherARM.Domain.Entities;
using DispatcherARM.Domain.Interfaces;
using DispatcherARM.UI.Views.Cards;
using Microsoft.Extensions.DependencyInjection;

namespace DispatcherARM.UI.Views.Pages;

public partial class VehiclesPage : UserControl
{
    public VehiclesPage() { InitializeComponent(); }

    private async void VehiclesPage_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            using var scope = App.Services.CreateScope();
            var vehicleRepo = scope.ServiceProvider.GetRequiredService<IVehicleRepository>();
            VehiclesGrid.ItemsSource = await vehicleRepo.GetAllAsync();
        }
        catch (Exception ex) { MessageBox.Show($"Ошибка: {ex.Message}"); }
    }

    private void VehiclesGrid_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (VehiclesGrid.SelectedItem is Vehicle vehicle)
        {
            var card = new VehicleCard(vehicle.Id);
            card.Owner = Window.GetWindow(this);
            card.ShowDialog();
        }
    }

    private async void AddVehicle_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new DispatcherARM.UI.Views.Dialogs.VehicleDialog();
        dialog.Owner = Window.GetWindow(this);
        if (dialog.ShowDialog() == true)
        {
            using var scope = App.Services.CreateScope();
            var vehicleRepo = scope.ServiceProvider.GetRequiredService<IVehicleRepository>();
            VehiclesGrid.ItemsSource = await vehicleRepo.GetAllAsync();
        }
    }
}
