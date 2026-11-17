using System.Windows;
using System.Windows.Controls;
using DispatcherARM.Domain.Enums;
using DispatcherARM.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace DispatcherARM.UI.Views.Pages;

/// <summary>
/// Панель управления (Dashboard) — КПД, прогресс-бары, последние заказы, водители, ТС
/// </summary>
public partial class DashboardPage : UserControl
{
    public DashboardPage()
    {
        InitializeComponent();
    }

    private async void DashboardPage_Loaded(object sender, RoutedEventArgs e)
    {
        TodayDateText.Text = DateTime.Now.ToString("dddd, d MMMM yyyy", new System.Globalization.CultureInfo("ru-RU"));
        await LoadDataAsync();
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await LoadDataAsync();

    private async Task LoadDataAsync()
    {
        LastUpdateText.Text = $"Обновлено: {DateTime.Now:HH:mm:ss}";
        try
        {
            using var scope = App.Services.CreateScope();
            var orderRepo  = scope.ServiceProvider.GetRequiredService<IOrderRepository>();
            var driverRepo = scope.ServiceProvider.GetRequiredService<IDriverRepository>();
            var vehicleRepo = scope.ServiceProvider.GetRequiredService<IVehicleRepository>();

            // ─── Заказы ──────────────────────────────────────────────────────
            var orders = (await orderRepo.GetAllAsync()).ToList();

            int total     = orders.Count;
            int newOrders = orders.Count(o => o.Status == OrderStatus.New);
            int assigned  = orders.Count(o => o.Status == OrderStatus.Assigned);
            int loading   = orders.Count(o => o.Status == OrderStatus.Loading);
            int transit   = orders.Count(o => o.Status == OrderStatus.InTransit);
            int delivered = orders.Count(o => o.Status == OrderStatus.Delivered);
            int cancelled = orders.Count(o => o.Status == OrderStatus.Cancelled);

            TotalOrdersText.Text     = total.ToString();
            NewOrdersText.Text       = newOrders.ToString();
            InTransitText.Text       = (transit + loading).ToString();
            DeliveredText.Text       = delivered.ToString();
            CancelledText.Text       = cancelled.ToString();

            // Сегодня
            var today = DateTime.Today;
            int todayCreated = orders.Count(o => o.CreatedAt.Date == today);
            TodayCreatedText.Text = todayCreated.ToString();

            // Прогресс-бары (ширина до 180px)
            const double barMax = 180.0;
            double setBar(int val) => total > 0 ? Math.Round(val * barMax / total) : 0;

            AssignedBar.Width  = setBar(assigned);
            TransitBar.Width   = setBar(transit + loading);
            DeliveredBar.Width = setBar(delivered);

            string pct(int v) => total > 0 ? $"{v * 100 / total}%" : "0%";
            AssignedPctText.Text  = pct(assigned);
            TransitPctText.Text   = pct(transit + loading);
            DeliveredPctText.Text = pct(delivered);

            double completionPct = total > 0 ? Math.Round(delivered * 100.0 / total, 0) : 0;
            CompletionPctText.Text = $"{completionPct}%";

            // Последние заказы
            var recentOrders = orders
                .OrderByDescending(o => o.CreatedAt)
                .Take(10)
                .Select(o => new RecentOrderRow
                {
                    OrderNumber    = o.OrderNumber,
                    ClientName     = o.ClientName,
                    DeliveryDateStr = o.DeliveryDate.HasValue
                        ? o.DeliveryDate.Value.ToString("dd.MM.yyyy")
                        : "—",
                    StatusDisplay  = o.Status switch
                    {
                        OrderStatus.New       => "Новый",
                        OrderStatus.Assigned  => "Назначен",
                        OrderStatus.Loading   => "Погрузка",
                        OrderStatus.InTransit => "В пути",
                        OrderStatus.Delivered => "Доставлен",
                        OrderStatus.Cancelled => "Отменён",
                        _ => o.Status.ToString()
                    },
                    DriverName = "—"
                })
                .ToList();
            RecentOrdersGrid.ItemsSource = recentOrders;

            // ─── Водители ────────────────────────────────────────────────────
            var drivers = (await driverRepo.GetAllAsync()).ToList();
            int availableDrivers = drivers.Count(d => d.Status == DriverStatus.Available);
            DriversAvailableText.Text = availableDrivers.ToString();

            var driverRows = drivers.Select(d => new DriverRow
            {
                FullName     = d.FullName,
                Phone        = d.Phone ?? "—",
                VehiclePlate = d.AssignedVehicle?.LicensePlate ?? "—",
                StatusDisplay = d.Status switch
                {
                    DriverStatus.Available  => "Свободен",
                    DriverStatus.OnRoute    => "В рейсе",
                    DriverStatus.OnLeave    => "В отпуске",
                    DriverStatus.Suspended  => "Отстранён",
                    _ => d.Status.ToString()
                }
            }).ToList();
            DriversGrid.ItemsSource = driverRows;

            // ─── Транспорт ───────────────────────────────────────────────────
            var vehicles = (await vehicleRepo.GetAllAsync()).ToList();
            var vehicleRows = vehicles.Select(v => new VehicleRow
            {
                LicensePlate = v.LicensePlate,
                Model        = v.Model,
                TypeDisplay  = v.Type switch
                {
                    VehicleType.Truck        => "Грузовик",
                    VehicleType.Van          => "Фургон",
                    VehicleType.Car          => "Легковой",
                    VehicleType.Refrigerator => "Рефрижератор",
                    _ => v.Type.ToString()
                },
                StatusDisplay = v.Status switch
                {
                    VehicleStatus.Available   => "Свободен",
                    VehicleStatus.OnRoute     => "В рейсе",
                    VehicleStatus.Maintenance => "ТО",
                    VehicleStatus.Unavailable => "Недоступен",
                    _ => v.Status.ToString()
                }
            }).ToList();
            VehiclesGrid.ItemsSource = vehicleRows;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Dashboard error: {ex.Message}");
        }
    }

    // ViewModel-строки для DataGrid
    public class RecentOrderRow
    {
        public string OrderNumber     { get; set; } = "";
        public string ClientName      { get; set; } = "";
        public string DeliveryDateStr { get; set; } = "";
        public string StatusDisplay   { get; set; } = "";
        public string DriverName      { get; set; } = "";
    }

    public class DriverRow
    {
        public string FullName      { get; set; } = "";
        public string Phone         { get; set; } = "";
        public string VehiclePlate  { get; set; } = "";
        public string StatusDisplay { get; set; } = "";
    }

    public class VehicleRow
    {
        public string LicensePlate  { get; set; } = "";
        public string Model         { get; set; } = "";
        public string TypeDisplay   { get; set; } = "";
        public string StatusDisplay { get; set; } = "";
    }
}
