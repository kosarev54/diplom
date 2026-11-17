using System.IO;
using System.Text;
using System.Windows;
using DispatcherARM.Domain.Entities;
using DispatcherARM.Domain.Enums;
using DispatcherARM.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace DispatcherARM.UI.Views.Dialogs;

/// <summary>Строка в таблице расчёта</summary>
public class RoutePayRow
{
    public DateTime  Date         { get; set; }
    public string    Name         { get; set; } = "";
    public double    TotalDistanceKm { get; set; }
    public int       WaypointsCount  { get; set; }
    public decimal   KmPay        { get; set; }
    public decimal   DeliveryPay  { get; set; }
    public decimal   TotalPay     => KmPay + DeliveryPay;
}

/// <summary>
/// Диалог расчёта и просмотра зарплаты водителя
/// </summary>
public partial class DriverPaymentDialog : Window
{
    private readonly Driver _driver;
    private List<RoutePayRow> _rows = new();

    public DriverPaymentDialog(Driver driver)
    {
        InitializeComponent();
        _driver = driver;
    }

    private async void Dialog_Loaded(object sender, RoutedEventArgs e)
    {
        TitleText.Text = $"Расчёт зарплаты: {_driver.FullName}";
        var now = DateTime.Now;
        DateFrom.SelectedDate = new DateTime(now.Year, now.Month, 1);
        DateTo.SelectedDate   = now;
        PeriodText.Text = $"за {now:MMMM yyyy}";
        await CalculateAsync();
    }

    private async void Recalculate_Click(object sender, RoutedEventArgs e) => await CalculateAsync();

    private async Task CalculateAsync()
    {
        try
        {
            var from = DateFrom.SelectedDate ?? new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            var to   = DateTo.SelectedDate   ?? DateTime.Now;
            PeriodText.Text = $"с {from:dd.MM.yyyy} по {to:dd.MM.yyyy}";

            using var scope = App.Services.CreateScope();
            var routeRepo = scope.ServiceProvider.GetRequiredService<IRouteRepository>();
            var allRoutes = await routeRepo.GetAllAsync();

            var driverRoutes = allRoutes
                .Where(r => r.DriverId == _driver.Id
                         && r.Date.Date >= from.Date
                         && r.Date.Date <= to.Date
                         && r.Status == RouteStatus.Completed)
                .ToList();

            _rows = new List<RoutePayRow>();
            foreach (var route in driverRoutes.OrderBy(r => r.Date))
            {
                var detailed = await routeRepo.GetRouteWithWaypointsAsync(route.Id);
                int wpCount = detailed?.Waypoints?.Count ?? 0;
                decimal kmPay       = (decimal)route.TotalDistanceKm * _driver.RatePerKm;
                decimal deliveryPay = wpCount * _driver.RatePerDelivery;

                _rows.Add(new RoutePayRow
                {
                    Date            = route.Date,
                    Name            = route.Name ?? $"Маршрут #{route.Id}",
                    TotalDistanceKm = route.TotalDistanceKm,
                    WaypointsCount  = wpCount,
                    KmPay           = kmPay,
                    DeliveryPay     = deliveryPay
                });
            }

            RoutesGrid.ItemsSource = _rows;

            // Итоги
            decimal totalKmPay    = _rows.Sum(r => r.KmPay);
            decimal totalDelivPay = _rows.Sum(r => r.DeliveryPay);

            // Пропорциональный оклад (дни в периоде)
            int periodDays   = (to - from).Days + 1;
            int calendarDays = DateTime.DaysInMonth(from.Year, from.Month);
            decimal salary   = _driver.BaseSalary * periodDays / calendarDays;

            decimal total = salary + totalKmPay + totalDelivPay;

            BaseText.Text    = $"{salary:N0} ₽";
            KmPayText.Text   = $"{totalKmPay:N0} ₽";
            DelivPayText.Text= $"{totalDelivPay:N0} ₽";
            TotalPayText.Text= $"{total:N0} ₽";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка расчёта: {ex.Message}");
        }
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter   = "CSV файлы (*.csv)|*.csv",
                FileName = $"salary_{_driver.FullName.Replace(" ", "_")}_{DateTime.Now:yyyyMM}.csv"
            };

            if (dialog.ShowDialog() != true) return;

            var sb = new StringBuilder();
            sb.AppendLine($"Расчёт зарплаты: {_driver.FullName}");
            sb.AppendLine($"Период: {DateFrom.SelectedDate:dd.MM.yyyy} — {DateTo.SelectedDate:dd.MM.yyyy}");
            sb.AppendLine();
            sb.AppendLine("Дата;Маршрут;Дистанция (км);Точек доставки;За км (₽);За доставки (₽);Итого (₽)");
            foreach (var row in _rows)
                sb.AppendLine($"{row.Date:dd.MM.yyyy};{row.Name};{row.TotalDistanceKm:F1};{row.WaypointsCount};{row.KmPay:N0};{row.DeliveryPay:N0};{row.TotalPay:N0}");
            sb.AppendLine();
            sb.AppendLine($"ИТОГО К ВЫПЛАТЕ;;{_rows.Sum(r => r.TotalDistanceKm):F1};{_rows.Sum(r => r.WaypointsCount)};" +
                          $"{_rows.Sum(r => r.KmPay):N0};{_rows.Sum(r => r.DeliveryPay):N0};{TotalPayText.Text}");

            File.WriteAllText(dialog.FileName, sb.ToString(), Encoding.UTF8);
            MessageBox.Show($"Экспортировано: {dialog.FileName}", "Готово",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка экспорта: {ex.Message}");
        }
    }
}
