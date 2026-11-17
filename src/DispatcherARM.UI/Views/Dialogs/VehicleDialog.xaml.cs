using System.Globalization;
using System.Windows;
using DispatcherARM.Domain.Entities;
using DispatcherARM.Domain.Enums;
using DispatcherARM.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace DispatcherARM.UI.Views.Dialogs;

public partial class VehicleDialog : Window
{
    public VehicleDialog() { InitializeComponent(); }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(PlateBox.Text) || string.IsNullOrWhiteSpace(ModelBox.Text))
        {
            ErrorText.Text = "Заполните обязательные поля";
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        double.TryParse(WeightBox.Text, CultureInfo.InvariantCulture, out double weight);
        double.TryParse(VolumeBox.Text, CultureInfo.InvariantCulture, out double volume);
        double.TryParse(FuelBox.Text, CultureInfo.InvariantCulture, out double fuel);

        try
        {
            using var scope = App.Services.CreateScope();
            var vehicleRepo = scope.ServiceProvider.GetRequiredService<IVehicleRepository>();

            var vehicle = new Vehicle
            {
                LicensePlate = PlateBox.Text.Trim(),
                Model = ModelBox.Text.Trim(),
                Type = (VehicleType)TypeBox.SelectedIndex,
                MaxWeightKg = weight,
                MaxVolumeM3 = volume,
                FuelConsumptionPer100Km = fuel
            };

            await vehicleRepo.AddAsync(vehicle);
            await vehicleRepo.SaveChangesAsync();
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            ErrorText.Text = $"Ошибка: {ex.Message}";
            ErrorText.Visibility = Visibility.Visible;
        }
    }
}
