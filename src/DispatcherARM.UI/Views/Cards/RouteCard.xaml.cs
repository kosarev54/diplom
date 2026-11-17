using System.Windows;
using DispatcherARM.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace DispatcherARM.UI.Views.Cards;

/// <summary>
/// Карточка маршрута
/// </summary>
public partial class RouteCard : Window
{
    private readonly int _routeId;

    public RouteCard(int routeId)
    {
        InitializeComponent();
        _routeId = routeId;
    }

    private async void RouteCard_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            using var scope = App.Services.CreateScope();
            var routeRepo = scope.ServiceProvider.GetRequiredService<IRouteRepository>();

            var route = await routeRepo.GetRouteWithWaypointsAsync(_routeId);
            if (route == null) { Close(); return; }

            // Шапка
            RouteTitleText.Text = route.Name ?? $"Маршрут #{route.Id}";
            RouteDriverText.Text = route.Driver?.FullName ?? "—";
            RouteVehicleText.Text = route.Vehicle?.LicensePlate ?? "—";
            RouteDateText.Text = route.Date.ToString("dd.MM.yyyy");

            DistanceText.Text = $"{route.TotalDistanceKm:F1} км";
            TimeText.Text = $"{route.TotalTimeMinutes:F0} мин";

            // Расход топлива
            double fuelUsed = route.Vehicle != null
                ? route.TotalDistanceKm * route.Vehicle.FuelConsumptionPer100Km / 100.0
                : 0;
            FuelText.Text = $"{fuelUsed:F1} л";

            // Выручка
            var totalRevenue = route.Waypoints?.Sum(w => w.Order?.DeliveryPrice ?? 0) ?? 0;
            RevenueText.Text = $"{totalRevenue:N0} ₽";

            // Вкладка 1: Параметры
            InfoDate.Text = route.Date.ToString("dd MMMM yyyy", new System.Globalization.CultureInfo("ru-RU"));
            InfoStatus.Text = route.Status.ToString();
            InfoDistance.Text = $"{route.TotalDistanceKm:F1} км";
            InfoTime.Text = $"{route.TotalTimeMinutes:F0} мин ({route.TotalTimeMinutes / 60:F1} ч)";
            InfoAlgorithm.Text = route.OptimizationMethod ?? "—";
            InfoFuelCalc.Text = route.FuelCostEstimate.HasValue ? $"{route.FuelCostEstimate:F1} л" : $"{fuelUsed:F1} л";

            // Водитель
            InfoDriver.Text = route.Driver?.FullName ?? "—";
            InfoDriverPhone.Text = route.Driver?.Phone ?? "";

            // ТС
            var vehicleType = route.Vehicle?.Type.ToString() ?? "";
            InfoVehicle.Text = route.Vehicle != null
                ? $"{route.Vehicle.LicensePlate} — {route.Vehicle.Model}"
                : "—";
            InfoVehicleType.Text = vehicleType;

            // Финансы
            decimal fuelPrice = 55m; // Цена топлива руб/л
            decimal fuelCostRub = (decimal)fuelUsed * fuelPrice;
            decimal profit = totalRevenue - fuelCostRub;

            FinRevenue.Text = $"{totalRevenue:N0} ₽";
            FinFuelCost.Text = $"{fuelCostRub:N0} ₽";
            FinProfit.Text = $"{profit:N0} ₽";

            // Вкладка 2: Точки
            var waypoints = route.Waypoints?.OrderBy(w => w.SequenceNumber).ToList() ?? new();
            WaypointSummary.Text = $"Точки доставки ({waypoints.Count} шт.) · " +
                $"Вес: {waypoints.Sum(w => w.Order?.Weight ?? 0):F0} кг · " +
                $"Выручка: {totalRevenue:N0} ₽";
            WaypointsGrid.ItemsSource = waypoints;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка загрузки маршрута: {ex.Message}");
        }
    }
}
