using System.Windows;
using DispatcherARM.Application.Services;
using DispatcherARM.Domain.Entities;
using DispatcherARM.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace DispatcherARM.UI.Views.Dialogs;

/// <summary>
/// Вспомогательный класс для привязки чекбокса
/// </summary>
public class SelectableOrder
{
    public bool IsSelected { get; set; }
    public Order Order { get; set; } = null!;
}

/// <summary>
/// Диалог построения оптимального маршрута
/// </summary>
public partial class BuildRouteDialog : Window
{
    public BuildRouteDialog()
    {
        InitializeComponent();
    }

    private async void Dialog_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            using var scope = App.Services.CreateScope();

            // Загрузить нераспределённые заказы
            var orderRepo = scope.ServiceProvider.GetRequiredService<IOrderRepository>();
            var orders = await orderRepo.GetUnassignedOrdersAsync();
            OrdersGrid.ItemsSource = orders.Select(o => new SelectableOrder { Order = o, IsSelected = true }).ToList();

            // Загрузить доступных водителей
            var driverRepo = scope.ServiceProvider.GetRequiredService<IDriverRepository>();
            var drivers = await driverRepo.GetAvailableDriversAsync();
            DriverBox.ItemsSource = drivers;
            if (drivers.Any()) DriverBox.SelectedIndex = 0;

            // Загрузить доступный транспорт
            var vehicleRepo = scope.ServiceProvider.GetRequiredService<IVehicleRepository>();
            var vehicles = await vehicleRepo.GetAvailableVehiclesAsync();
            VehicleBox.ItemsSource = vehicles;
            if (vehicles.Any()) VehicleBox.SelectedIndex = 0;

            // Алгоритмы
            var routeService = scope.ServiceProvider.GetRequiredService<RouteService>();
            var algorithms = routeService.GetAvailableAlgorithms().ToList();
            AlgorithmBox.ItemsSource = algorithms;
            if (algorithms.Any()) AlgorithmBox.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            ErrorText.Text = $"Ошибка загрузки: {ex.Message}";
        }
    }

    private async void BuildButton_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Text = "";
        ResultPanel.Visibility = Visibility.Collapsed;

        // Валидация
        var selectedOrders = (OrdersGrid.ItemsSource as IEnumerable<SelectableOrder>)?
            .Where(s => s.IsSelected).ToList();

        if (selectedOrders == null || selectedOrders.Count == 0)
        {
            ErrorText.Text = "Выберите хотя бы один заказ";
            return;
        }
        if (DriverBox.SelectedItem is not Driver driver)
        {
            ErrorText.Text = "Выберите водителя";
            return;
        }
        if (VehicleBox.SelectedItem is not Vehicle vehicle)
        {
            ErrorText.Text = "Выберите транспорт";
            return;
        }
        if (AlgorithmBox.SelectedItem is not string algorithm)
        {
            ErrorText.Text = "Выберите алгоритм";
            return;
        }

        BuildButton.IsEnabled = false;
        Progress.Visibility = Visibility.Visible;

        try
        {
            using var scope = App.Services.CreateScope();
            var routeService = scope.ServiceProvider.GetRequiredService<RouteService>();

            var orderIds = selectedOrders.Select(s => s.Order.Id);
            var (route, result) = await routeService.BuildRouteAsync(
                orderIds, driver.Id, vehicle.Id, algorithm);

            if (result.Success && route != null)
            {
                ResultPanel.Visibility = Visibility.Visible;
                ResultText.Text = $"✅ Маршрут построен!\n\n" +
                    $"📏 Дистанция: {result.TotalDistanceKm:F1} км\n" +
                    $"⏱ Время: {result.TotalTimeMinutes:F0} мин\n" +
                    $"⛽ Расход топлива: {route.FuelCostEstimate:F1} л\n" +
                    $"📦 Точек доставки: {result.OrderedWaypoints.Count}\n" +
                    $"🔧 Алгоритм: {result.AlgorithmName}\n" +
                    $"⚡ Время расчёта: {result.ElapsedMilliseconds} мс";

                DialogResult = true;
            }
            else
            {
                ErrorText.Text = result.ErrorMessage ?? "Не удалось построить маршрут";
            }
        }
        catch (Exception ex)
        {
            ErrorText.Text = $"Ошибка: {ex.Message}";
        }
        finally
        {
            BuildButton.IsEnabled = true;
            Progress.Visibility = Visibility.Collapsed;
        }
    }
}
