using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using DispatcherARM.Application.Services;
using DispatcherARM.Domain.Entities;
using DispatcherARM.Domain.Enums;
using DispatcherARM.Domain.Interfaces;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;

namespace DispatcherARM.UI.Views.Pages;

/// <summary>
/// Страница отчётов и аналитики
/// </summary>
public partial class ReportsPage : UserControl
{
    private List<Order> _allOrders = new();
    private List<Driver> _allDrivers = new();
    private List<Vehicle> _allVehicles = new();

    public ReportsPage()
    {
        InitializeComponent();
    }

    private async void ReportsPage_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            using var scope = App.Services.CreateScope();
            var orderRepo = scope.ServiceProvider.GetRequiredService<IOrderRepository>();
            var driverRepo = scope.ServiceProvider.GetRequiredService<IDriverRepository>();
            var vehicleRepo = scope.ServiceProvider.GetRequiredService<IVehicleRepository>();

            _allOrders = (await orderRepo.GetAllAsync()).ToList();
            _allDrivers = (await driverRepo.GetAllAsync()).ToList();
            _allVehicles = (await vehicleRepo.GetAllAsync()).ToList();

            BuildStatusChart();
            BuildPriorityChart();
            BuildStatsTable();
            await BuildPayrollReportAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ошибка загрузки отчётов: {ex.Message}");
        }
    }

    // ─── Зарплатная ведомость ────────────────────────────────────────────────────
    private async Task BuildPayrollReportAsync()
    {
        try
        {
            using var scope = App.Services.CreateScope();
            var txRepo     = scope.ServiceProvider.GetRequiredService<ITransactionRepository>();
            var orderRepo  = scope.ServiceProvider.GetRequiredService<IOrderRepository>();
            var allOrders  = (await orderRepo.GetAllAsync()).ToList();

            var now   = DateTime.Now;
            var rows  = new List<PayrollRow>();

            foreach (var driver in _allDrivers)
            {
                var driverOrders = allOrders
                    .Where(o => o.AssignedDriverId == driver.Id
                             && o.Status == OrderStatus.Delivered
                             && o.CompletedAt.HasValue
                             && o.CompletedAt.Value.Month == now.Month
                             && o.CompletedAt.Value.Year  == now.Year)
                    .ToList();

                var txs       = (await txRepo.GetByDriverIdAsync(driver.Id)).ToList();
                var earned    = txs.Where(t => t.Type == TransactionType.Earning).AsEnumerable().Sum(t => t.Amount);
                var withdrawn = txs.Where(t => t.Type == TransactionType.Withdrawal).AsEnumerable().Sum(t => t.Amount);
                var balance   = earned - withdrawn;

                // Доп. выплаты за этот месяц
                var monthEarnings = txs
                    .Where(t => t.Type == TransactionType.Earning
                              && t.CreatedAt.Month == now.Month
                              && t.CreatedAt.Year  == now.Year)
                    .AsEnumerable().Sum(t => t.Amount);

                var totalKm = driverOrders.Count * 25.0; // приблизительно, можно улучшить через маршруты

                rows.Add(new PayrollRow
                {
                    FullName        = driver.FullName,
                    Phone           = driver.Phone,
                    WorkSchedule    = driver.WorkSchedule ?? "—",
                    OrdersThisMonth = driverOrders.Count,
                    KmThisMonth     = totalKm,
                    BaseSalary      = driver.BaseSalary,
                    Bonuses         = monthEarnings,
                    TotalPayment    = driver.BaseSalary + monthEarnings,
                    Balance         = balance
                });
            }

            if (PayrollGrid != null)
                PayrollGrid.ItemsSource = rows;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ошибка ведомости: {ex.Message}");
        }
    }

    private async void ExportPayrollCsv_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter   = "CSV файлы (*.csv)|*.csv",
            FileName = $"Зарплатная_ведомость_{DateTime.Now:yyyy_MM}.csv"
        };
        if (dialog.ShowDialog() != true) return;

        try
        {
            using var scope = App.Services.CreateScope();
            var txRepo    = scope.ServiceProvider.GetRequiredService<ITransactionRepository>();
            var orderRepo = scope.ServiceProvider.GetRequiredService<IOrderRepository>();
            var allOrders = (await orderRepo.GetAllAsync()).ToList();
            var now       = DateTime.Now;

            var sb = new StringBuilder();
            sb.AppendLine("ФИО;Телефон;График;Заказов за месяц;Оклад (руб);Доп. выплаты (руб);ИТОГО К ВЫПЛАТЕ (руб);Баланс на счёте (руб)");

            foreach (var driver in _allDrivers)
            {
                var driverOrders = allOrders.Where(o =>
                    o.AssignedDriverId == driver.Id &&
                    o.Status == OrderStatus.Delivered &&
                    o.CompletedAt?.Month == now.Month &&
                    o.CompletedAt?.Year  == now.Year).ToList();

                var txs      = (await txRepo.GetByDriverIdAsync(driver.Id)).ToList();
                var earned   = txs.Where(t => t.Type == TransactionType.Earning
                                           && t.CreatedAt.Month == now.Month
                                           && t.CreatedAt.Year  == now.Year).Sum(t => t.Amount);
                var total    = driver.BaseSalary + earned;
                var balance  = txs.Where(t => t.Type == TransactionType.Earning).Sum(t => t.Amount)
                             - txs.Where(t => t.Type == TransactionType.Withdrawal).Sum(t => t.Amount);

                sb.AppendLine($"{driver.FullName};{driver.Phone};{driver.WorkSchedule ?? "—"};" +
                              $"{driverOrders.Count};{driver.BaseSalary:F2};{earned:F2};{total:F2};{balance:F2}");
            }

            File.WriteAllText(dialog.FileName, sb.ToString(), Encoding.UTF8);
            MessageBox.Show($"Ведомость экспортирована:\n{dialog.FileName}", "Готово",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка: {ex.Message}");
        }
    }

    private void BuildStatusChart()
    {
        var statusGroups = _allOrders.GroupBy(o => o.Status).ToList();
        var colors = new[] {
            new SKColor(33, 150, 243),   // New - синий
            new SKColor(255, 152, 0),    // Assigned - оранжевый
            new SKColor(0, 188, 212),    // InTransit - голубой
            new SKColor(76, 175, 80),    // Delivered - зелёный
            new SKColor(244, 67, 54)     // Cancelled - красный
        };
        var names = new[] { "Новые", "Назначенные", "В пути", "Доставленные", "Отменённые" };

        var series = new List<ISeries>();
        foreach (var group in statusGroups)
        {
            int idx = (int)group.Key;
            series.Add(new PieSeries<int>
            {
                Values = new[] { group.Count() },
                Name = idx < names.Length ? names[idx] : group.Key.ToString(),
                Fill = idx < colors.Length ? new SolidColorPaint(colors[idx]) : null
            });
        }

        StatusPieChart.Series = series;
    }

    private void BuildPriorityChart()
    {
        var priorityGroups = _allOrders.GroupBy(o => o.Priority).ToList();
        var colors = new[] {
            new SKColor(158, 158, 158),  // Low - серый
            new SKColor(33, 150, 243),   // Normal - синий
            new SKColor(255, 152, 0),    // High - оранжевый
            new SKColor(244, 67, 54)     // Urgent - красный
        };
        var names = new[] { "Низкий", "Обычный", "Высокий", "Срочный" };

        var series = new List<ISeries>();
        foreach (var group in priorityGroups)
        {
            int idx = (int)group.Key;
            series.Add(new PieSeries<int>
            {
                Values = new[] { group.Count() },
                Name = idx < names.Length ? names[idx] : group.Key.ToString(),
                Fill = idx < colors.Length ? new SolidColorPaint(colors[idx]) : null
            });
        }

        PriorityPieChart.Series = series;
    }

    private void BuildStatsTable()
    {
        var stats = new List<object>
        {
            new { Metric = "Всего заказов", Value = _allOrders.Count.ToString() },
            new { Metric = "Новых заказов", Value = _allOrders.Count(o => o.Status == OrderStatus.New).ToString() },
            new { Metric = "Доставлено", Value = _allOrders.Count(o => o.Status == OrderStatus.Delivered).ToString() },
            new { Metric = "Общий вес грузов (кг)", Value = _allOrders.Sum(o => o.Weight).ToString("F1") },
            new { Metric = "Общий объём грузов (м³)", Value = _allOrders.Sum(o => o.Volume).ToString("F2") },
            new { Metric = "Средний вес заказа (кг)", Value = (_allOrders.Any() ? _allOrders.Average(o => o.Weight) : 0).ToString("F1") },
            new { Metric = "Водителей", Value = _allDrivers.Count.ToString() },
            new { Metric = "Доступных водителей", Value = _allDrivers.Count(d => d.Status == DriverStatus.Available).ToString() },
            new { Metric = "Транспортных средств", Value = _allVehicles.Count.ToString() },
            new { Metric = "Доступных ТС", Value = _allVehicles.Count(v => v.Status == VehicleStatus.Available).ToString() },
        };

        StatsGrid.ItemsSource = stats;
    }

    private void CompareAlgorithms_Click(object sender, RoutedEventArgs e)
    {
        // ── Этап 4 диплома: полноценный диалог сравнения алгоритмов ────────────
        var dlg = new DispatcherARM.UI.Views.Dialogs.AlgorithmCompareDialog
        {
            Owner = Window.GetWindow(this)
        };
        dlg.ShowDialog();
    }

    private void ExportCsv_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "CSV файлы (*.csv)|*.csv",
                FileName = $"orders_report_{DateTime.Now:yyyyMMdd}.csv"
            };

            if (dialog.ShowDialog() == true)
            {
                var sb = new StringBuilder();
                sb.AppendLine("Номер;Клиент;Адрес;Вес;Объём;Приоритет;Статус;Дата доставки");

                foreach (var order in _allOrders)
                {
                    sb.AppendLine($"{order.OrderNumber};{order.ClientName};{order.Address};" +
                        $"{order.Weight};{order.Volume};{order.Priority};{order.Status};" +
                        $"{order.DeliveryDate:dd.MM.yyyy}");
                }

                File.WriteAllText(dialog.FileName, sb.ToString(), Encoding.UTF8);
                MessageBox.Show($"Отчёт экспортирован: {dialog.FileName}", "Экспорт",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка экспорта: {ex.Message}", "Ошибка");
        }
    }

}

/// <summary>Строка зарплатной ведомости</summary>
public class PayrollRow
{
    public string  FullName        { get; set; } = "";
    public string  Phone           { get; set; } = "";
    public string  WorkSchedule    { get; set; } = "";
    public int     OrdersThisMonth { get; set; }
    public double  KmThisMonth     { get; set; }
    public decimal BaseSalary      { get; set; }
    public decimal Bonuses         { get; set; }
    public decimal TotalPayment    { get; set; }
    public decimal Balance         { get; set; }
}
