using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using DispatcherARM.Domain.Entities;
using DispatcherARM.Domain.Enums;
using DispatcherARM.Domain.Interfaces;
using DispatcherARM.UI.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DispatcherARM.UI.Views.Dialogs;

/// <summary>
/// Диалог сравнения алгоритмов маршрутизации — научная часть диплома.
/// Запускает все три оптимизатора на одном наборе заказов и выводит сравнительную таблицу.
/// </summary>
public partial class AlgorithmCompareDialog : Window
{
    // ── Строка результата для DataGrid ──────────────────────────────────────
    private class ResultRow
    {
        public string AlgorithmName { get; set; } = "";
        public string DistanceKm    { get; set; } = "—";
        public string TimeMinutes   { get; set; } = "—";
        public string OrderCount    { get; set; } = "—";
        public string FuelLiters    { get; set; } = "—";
        public string CalcMs        { get; set; } = "—";
        public string DeviationPct  { get; set; } = "—";
        public string Status        { get; set; } = "";
        // Для экспорта
        public double RawDistance   { get; set; }
        public double RawTime       { get; set; }
    }

    // ── Тестовые сценарии для диплома ────────────────────────────────────────
    private class ScenarioRow
    {
        public string Name     { get; set; } = "";
        public string N        { get; set; } = "";
        public string Desc     { get; set; } = "";
        public string Expected { get; set; } = "";
    }

    private List<Order>   _currentOrders   = new();
    private List<ResultRow> _lastResults   = new();

    public AlgorithmCompareDialog()
    {
        InitializeComponent();
        Loaded += OnLoaded;

        // Статические сценарии для диплома
        ScenariosGrid.ItemsSource = new List<ScenarioRow>
        {
            new() { Name = "Малый",    N = "5–10",  Desc = "Центр города, близкие адреса",           Expected = "Все алгоритмы дают схожий результат" },
            new() { Name = "Средний",  N = "15–20", Desc = "Разные районы Москвы",                   Expected = "2-opt улучшает NN на 5–15%, OSRM точнее" },
            new() { Name = "Большой",  N = "30–50", Desc = "Всё Подмосковье, разная плотность",      Expected = "Разница 2-opt vs NN до 20%, OSRM может не ответить" },
            new() { Name = "Приоритет",N = "10",    Desc = "Срочные заказы — должны быть первыми",   Expected = "OSRM учитывает дорожную сеть лучше" },
            new() { Name = "Загрузка", N = "15",    Desc = "На грани грузоподъёмности ТС",           Expected = "Все алгоритмы выдают допустимый маршрут" },
        };
    }

    // ── Загрузка: список ТС ──────────────────────────────────────────────────
    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            using var scope = App.Services.CreateScope();
            var vehicleRepo = scope.ServiceProvider.GetRequiredService<IVehicleRepository>();
            var vehicles    = await vehicleRepo.GetAllAsync();

            VehicleCombo.ItemsSource = vehicles
                .Where(v => v.Status == VehicleStatus.Available)
                .Select(v => new { v.Id, DisplayName = $"{v.LicensePlate} — {v.Model} ({v.MaxWeightKg} кг)" })
                .ToList();
            VehicleCombo.DisplayMemberPath = "DisplayName";

            if (VehicleCombo.Items.Count > 0) VehicleCombo.SelectedIndex = 0;

            await RefreshOrderCount("all");
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Ошибка загрузки данных: {ex.Message}";
        }
    }

    private async void OrderSetCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (OrderSetCombo.SelectedItem is ComboBoxItem item)
            await RefreshOrderCount(item.Tag?.ToString() ?? "all");
    }

    private async Task RefreshOrderCount(string tag)
    {
        try
        {
            using var scope = App.Services.CreateScope();
            var repo   = scope.ServiceProvider.GetRequiredService<IOrderRepository>();
            var orders = await repo.GetAllAsync();

            _currentOrders = tag switch
            {
                "10"  => orders.Where(o => o.Status != OrderStatus.Delivered && o.Status != OrderStatus.Cancelled)
                               .OrderByDescending(o => o.CreatedAt).Take(10).ToList(),
                "20"  => orders.Where(o => o.Status != OrderStatus.Delivered && o.Status != OrderStatus.Cancelled)
                               .OrderByDescending(o => o.CreatedAt).Take(20).ToList(),
                "50"  => orders.Where(o => o.Status != OrderStatus.Delivered && o.Status != OrderStatus.Cancelled)
                               .OrderByDescending(o => o.CreatedAt).Take(50).ToList(),
                _     => orders.Where(o => o.Status == OrderStatus.New || o.Status == OrderStatus.Assigned).ToList()
            };

            OrderCountText.Text = $"Заказов: {_currentOrders.Count}";
        }
        catch { }
    }

    // ── Запуск сравнения ──────────────────────────────────────────────────────
    private async void RunComparison_Click(object sender, RoutedEventArgs e)
    {
        if (_currentOrders.Count < 2)
        {
            StatusText.Text = "⚠ Нужно минимум 2 заказа для сравнения.";
            return;
        }

        dynamic? selectedVehicle = VehicleCombo.SelectedItem;
        if (selectedVehicle == null)
        {
            StatusText.Text = "⚠ Выберите транспортное средство.";
            return;
        }

        // Показываем прогресс
        RunBtn.IsEnabled    = false;
        ExportBtn.IsEnabled = false;
        ProgressPanel.Visibility = Visibility.Visible;
        SummaryBorder.Visibility = Visibility.Collapsed;
        ResultsGrid.ItemsSource  = null;
        StatusText.Text = "Запущено сравнение алгоритмов…";

        try
        {
            int vehicleId = (int)selectedVehicle.Id;

            using var scope  = App.Services.CreateScope();
            var vehicleRepo  = scope.ServiceProvider.GetRequiredService<IVehicleRepository>();
            var vehicle      = await vehicleRepo.GetByIdAsync(vehicleId);
            if (vehicle == null) { StatusText.Text = "ТС не найдено"; return; }

            var optimizers   = scope.ServiceProvider.GetServices<DispatcherARM.Domain.Interfaces.IRouteOptimizer>().ToList();
            var depot        = (AppSettings.Instance.DepotLat, AppSettings.Instance.DepotLon);

            _lastResults = new List<ResultRow>();

            foreach (var optimizer in optimizers)
            {
                ProgressText.Text = $"Выполняется: {optimizer.AlgorithmName}…";

                var sw = Stopwatch.StartNew();
                DispatcherARM.Domain.Models.RouteOptimizationResult result;
                try
                {
                    result = await optimizer.OptimizeAsync(_currentOrders, vehicle, depot);
                }
                catch (Exception ex)
                {
                    _lastResults.Add(new ResultRow
                    {
                        AlgorithmName = optimizer.AlgorithmName,
                        Status = $"Ошибка: {ex.Message}"
                    });
                    continue;
                }
                sw.Stop();

                _lastResults.Add(new ResultRow
                {
                    AlgorithmName = optimizer.AlgorithmName,
                    DistanceKm    = result.Success ? $"{result.TotalDistanceKm:F2}" : "—",
                    TimeMinutes   = result.Success ? $"{result.TotalTimeMinutes:F1}" : "—",
                    OrderCount    = result.Success ? result.OrderedWaypoints.Count.ToString() : "—",
                    FuelLiters    = result.Success
                        ? $"{result.TotalDistanceKm * vehicle.FuelConsumptionPer100Km / 100.0:F2}" : "—",
                    CalcMs        = sw.ElapsedMilliseconds.ToString(),
                    Status        = result.Success ? "✅ Успех" : $"❌ {result.ErrorMessage}",
                    RawDistance   = result.Success ? result.TotalDistanceKm : double.MaxValue,
                    RawTime       = result.Success ? result.TotalTimeMinutes : double.MaxValue
                });
            }

            // Вычисляем отклонение от лучшего результата
            double bestDist = _lastResults.Where(r => r.RawDistance < double.MaxValue)
                                          .Select(r => r.RawDistance)
                                          .DefaultIfEmpty(0).Min();

            foreach (var row in _lastResults)
            {
                if (row.RawDistance < double.MaxValue && bestDist > 0)
                {
                    double pct = (row.RawDistance - bestDist) / bestDist * 100.0;
                    row.DeviationPct = pct < 0.01 ? "✅ Лучший" : $"+{pct:F1}%";
                }
            }

            ResultsGrid.ItemsSource = _lastResults;

            // Генерируем интерпретацию
            BuildSummary(bestDist, vehicle);

            ExportBtn.IsEnabled = true;
            StatusText.Text = $"Сравнение завершено. Алгоритмов: {_lastResults.Count} | Заказов: {_currentOrders.Count} | ТС: {vehicle.Model}";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Ошибка: {ex.Message}";
            AppLogger.Error("AlgorithmCompare error", ex);
        }
        finally
        {
            RunBtn.IsEnabled = true;
            ProgressPanel.Visibility = Visibility.Collapsed;
        }
    }

    // ── Генерация текстовой интерпретации ────────────────────────────────────
    private void BuildSummary(double bestDist, Vehicle vehicle)
    {
        var sb = new StringBuilder();
        var successRows = _lastResults.Where(r => r.RawDistance < double.MaxValue).ToList();
        if (!successRows.Any()) { SummaryBorder.Visibility = Visibility.Collapsed; return; }

        var best  = successRows.OrderBy(r => r.RawDistance).First();
        var worst = successRows.OrderByDescending(r => r.RawDistance).First();
        double improvement = worst.RawDistance > 0
            ? (worst.RawDistance - best.RawDistance) / worst.RawDistance * 100.0
            : 0;

        sb.AppendLine($"📊 Эксперимент: {_currentOrders.Count} заказов, ТС «{vehicle.Model}» (до {vehicle.MaxWeightKg} кг, расход {vehicle.FuelConsumptionPer100Km} л/100 км).");
        sb.AppendLine();
        sb.AppendLine($"🏆 Наилучший результат: {best.AlgorithmName} — {best.DistanceKm} км, {best.TimeMinutes} мин.");
        sb.AppendLine($"📉 Наихудший результат: {worst.AlgorithmName} — {worst.DistanceKm} км.");
        if (improvement > 0.5)
            sb.AppendLine($"💡 Выигрыш лучшего алгоритма над худшим: {improvement:F1}% по длине маршрута.");

        // Конкретные рекомендации
        sb.AppendLine();
        sb.AppendLine("Рекомендации для практики:");
        if (_currentOrders.Count <= 10)
            sb.AppendLine("• При малом числе точек (≤10) все алгоритмы дают близкий результат; выбор по скорости расчёта.");
        if (_currentOrders.Count > 10)
            sb.AppendLine("• При большом числе точек (>10) 2-opt существенно улучшает ближайшего соседа.");

        sb.AppendLine("• OSRM использует реальную дорожную сеть — его результат наиболее точен при наличии интернета.");
        sb.AppendLine("• Ближайший сосед — самый быстрый, подходит для оперативного планирования.");
        sb.AppendLine("• 2-opt — компромисс: точнее NN, не требует внешнего API.");

        double fuelBest = best.RawDistance * vehicle.FuelConsumptionPer100Km / 100.0;
        double fuelCost = fuelBest * AppSettings.Instance.FuelPricePerLiter;
        sb.AppendLine();
        sb.AppendLine($"⛽ Расчётный расход топлива (лучший маршрут): {fuelBest:F1} л ≈ {fuelCost:N0} ₽ " +
                      $"(цена топлива: {AppSettings.Instance.FuelPricePerLiter:F0} ₽/л, задаётся в Настройках).");

        SummaryText.Text = sb.ToString();
        SummaryBorder.Visibility = Visibility.Visible;
    }

    // ── Экспорт в CSV ─────────────────────────────────────────────────────────
    private void Export_Click(object sender, RoutedEventArgs e)
    {
        if (!_lastResults.Any()) return;

        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            FileName = $"AlgorithmCompare_{DateTime.Now:yyyyMMdd_HHmm}.csv",
            Filter   = "CSV файлы (*.csv)|*.csv"
        };
        if (dlg.ShowDialog() != true) return;

        var sb = new StringBuilder();
        sb.AppendLine("Алгоритм;Маршрут (км);Время (мин);Заказов;Топливо (л);Расчёт (мс);Откл. от лучшего;Статус");
        foreach (var r in _lastResults)
            sb.AppendLine($"{r.AlgorithmName};{r.DistanceKm};{r.TimeMinutes};{r.OrderCount};{r.FuelLiters};{r.CalcMs};{r.DeviationPct};{r.Status}");

        sb.AppendLine();
        sb.AppendLine($"Дата;{DateTime.Now:dd.MM.yyyy HH:mm}");
        sb.AppendLine($"Заказов;{_currentOrders.Count}");
        sb.AppendLine($"Депо;{AppSettings.Instance.DepotAddress}");

        File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
        MessageBox.Show($"Экспортировано в:\n{dlg.FileName}", "Готово",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
