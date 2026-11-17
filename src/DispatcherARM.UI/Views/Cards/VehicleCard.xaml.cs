using System.Windows;
using System.Windows.Media;
using DispatcherARM.Domain.Entities;
using DispatcherARM.Domain.Enums;
using DispatcherARM.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace DispatcherARM.UI.Views.Cards;

/// <summary>
/// Карточка транспортного средства
/// </summary>
public partial class VehicleCard : Window
{
    private readonly int _vehicleId;

    public VehicleCard(int vehicleId)
    {
        InitializeComponent();
        _vehicleId = vehicleId;
    }

    private async void VehicleCard_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            using var scope = App.Services.CreateScope();
            var vehicleRepo = scope.ServiceProvider.GetRequiredService<IVehicleRepository>();
            var routeRepo = scope.ServiceProvider.GetRequiredService<IRouteRepository>();

            var vehicle = await vehicleRepo.GetByIdAsync(_vehicleId);
            if (vehicle == null) { Close(); return; }

            // Шапка
            VehicleTitleText.Text = $"{vehicle.LicensePlate} — {vehicle.Model}";
            TypeText.Text = vehicle.Type switch
            {
                VehicleType.Car => "Легковой",
                VehicleType.Van => "Фургон",
                VehicleType.Truck => "Грузовик",
                VehicleType.Refrigerator => "Рефрижератор",
                _ => vehicle.Type.ToString()
            };

            switch (vehicle.Status)
            {
                case VehicleStatus.Available:
                    StatusText.Text = "Доступно";
                    StatusIcon.Foreground = new SolidColorBrush(Colors.LightGreen);
                    break;
                case VehicleStatus.OnRoute:
                    StatusText.Text = "В рейсе";
                    StatusIcon.Foreground = new SolidColorBrush(Colors.Orange);
                    break;
                case VehicleStatus.Maintenance:
                    StatusText.Text = "На ТО";
                    StatusIcon.Foreground = new SolidColorBrush(Colors.Yellow);
                    break;
                default:
                    StatusText.Text = "Недоступно";
                    StatusIcon.Foreground = new SolidColorBrush(Colors.Red);
                    break;
            }

            OdometerText.Text = $"{vehicle.OdometerKm:N0} км";
            LastMaintText.Text = vehicle.LastMaintenanceDate.HasValue
                ? vehicle.LastMaintenanceDate.Value.ToString("dd.MM.yy")
                : "—";

            // Вкладка 1: Основные данные
            InfoPlate.Text = vehicle.LicensePlate;
            InfoModel.Text = vehicle.Model;
            InfoYear.Text = vehicle.Year?.ToString() ?? "—";
            InfoVin.Text = vehicle.Vin ?? "—";
            InfoType.Text = TypeText.Text;
            InfoStatus.Text = StatusText.Text;

            InfoWeight.Text = $"{vehicle.MaxWeightKg:N0} кг";
            InfoVolume.Text = $"{vehicle.MaxVolumeM3:F1} м³";
            InfoFuel.Text = $"{vehicle.FuelConsumptionPer100Km:F1} л/100км";
            InfoOdometer.Text = $"{vehicle.OdometerKm:N0} км";
            InfoInsurance.Text = vehicle.InsurancePolicy != null
                ? $"{vehicle.InsurancePolicy} (до {vehicle.InsuranceExpiry:dd.MM.yyyy})"
                : "—";
            InfoCost.Text = vehicle.MonthlyCost.HasValue ? $"{vehicle.MonthlyCost:N0} ₽/мес" : "—";

            // ТО
            LastToDate.Text = vehicle.LastMaintenanceDate.HasValue
                ? vehicle.LastMaintenanceDate.Value.ToString("dd.MM.yyyy")
                : "Нет данных";
            LastToOdometer.Text = vehicle.LastMaintenanceOdometerKm.HasValue
                ? $"{vehicle.LastMaintenanceOdometerKm:N0} км"
                : "—";
            NextToDate.Text = vehicle.NextMaintenanceDate.HasValue
                ? vehicle.NextMaintenanceDate.Value.ToString("dd.MM.yyyy")
                : "—";

            if (vehicle.NextMaintenanceKm.HasValue)
            {
                double remaining = vehicle.NextMaintenanceKm.Value - vehicle.OdometerKm
                    + (vehicle.LastMaintenanceOdometerKm ?? 0);
                NextToKm.Text = remaining > 0 ? $"{remaining:N0} км" : "⚠ Требуется ТО!";
                if (remaining <= 500)
                    NextToCard.Background = new SolidColorBrush(Color.FromRgb(255, 235, 238));
            }
            else
            {
                NextToKm.Text = "—";
            }

            // Вкладка 2: Маршруты
            var allRoutes = await routeRepo.GetAllAsync();
            var vehicleRoutes = allRoutes
                .Where(r => r.VehicleId == _vehicleId)
                .OrderByDescending(r => r.Date)
                .ToList();

            RoutesCountText.Text = vehicleRoutes.Count.ToString();
            RoutesGrid.ItemsSource = vehicleRoutes;

            // Последний водитель
            var lastRoute = vehicleRoutes.FirstOrDefault();
            if (lastRoute != null)
            {
                var detailedRoute = await routeRepo.GetRouteWithWaypointsAsync(lastRoute.Id);
                LastDriverText.Text = detailedRoute?.Driver?.FullName ?? "—";
                LastRouteText.Text = $"Маршрут: {detailedRoute?.Name ?? "—"} · {lastRoute.Date:dd.MM.yyyy} · {lastRoute.TotalDistanceKm:F1} км";
            }
            else
            {
                LastDriverText.Text = "Нет данных";
                LastRouteText.Text = "Маршруты не найдены";
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка загрузки карточки ТС: {ex.Message}");
        }
    }
}
