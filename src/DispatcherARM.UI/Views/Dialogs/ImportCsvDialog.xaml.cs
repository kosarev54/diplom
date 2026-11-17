using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Media;
using DispatcherARM.Domain.Entities;
using DispatcherARM.Domain.Enums;
using DispatcherARM.Domain.Interfaces;
using DispatcherARM.UI.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DispatcherARM.UI.Views.Dialogs;

public partial class ImportCsvDialog : Window
{
    private string? _filePath;
    private readonly List<Order> _parsedOrders = new();
    public int ImportedCount { get; private set; }

    public ImportCsvDialog() { InitializeComponent(); }

    // ─── Выбор файла ─────────────────────────────────────────────────────────────
    private void SelectFile_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "CSV файлы (*.csv)|*.csv|Все файлы (*.*)|*.*",
            Title  = "Выберите CSV файл с заказами"
        };
        if (dlg.ShowDialog() != true) return;
        _filePath = dlg.FileName;
        FilePathText.Text = $"📄 {Path.GetFileName(_filePath)}  ({new FileInfo(_filePath).Length / 1024.0:F1} КБ)";
        ParseFile();
    }

    // ─── Парсинг и предпросмотр ──────────────────────────────────────────────────
    private void ParseFile()
    {
        if (_filePath == null) return;
        _parsedOrders.Clear();
        var errors = new List<string>();

        try
        {
            var lines = File.ReadAllLines(_filePath, Encoding.UTF8);
            if (lines.Length < 2) { ShowResult("⚠️ Файл пустой или содержит только заголовок", false); return; }

            int idx = 0;
            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;

                var c = line.Split(';');
                if (c.Length < 3) { errors.Add($"Строка {i + 1}: недостаточно колонок (минимум 3)"); continue; }

                try
                {
                    double.TryParse(c.Length > 3 ? c[3].Trim() : "0", CultureInfo.InvariantCulture, out double lat);
                    double.TryParse(c.Length > 4 ? c[4].Trim() : "0", CultureInfo.InvariantCulture, out double lon);
                    double.TryParse(c.Length > 5 ? c[5].Trim() : "0", CultureInfo.InvariantCulture, out double weight);
                    double.TryParse(c.Length > 6 ? c[6].Trim() : "0", CultureInfo.InvariantCulture, out double volume);
                    decimal.TryParse(c.Length > 7 ? c[7].Trim() : "0", CultureInfo.InvariantCulture, out decimal price);
                    decimal.TryParse(c.Length > 8 ? c[8].Trim() : "0", CultureInfo.InvariantCulture, out decimal cod);
                    var priority = c.Length > 9 && Enum.TryParse<OrderPriority>(c[9].Trim(), true, out var p)
                        ? p : OrderPriority.Normal;

                    // Если координаты не заданы — ставим депо (Москва)
                    if (lat == 0 && lon == 0) { lat = AppSettings.Instance.DepotLat; lon = AppSettings.Instance.DepotLon; }

                    idx++;
                    var date = DeliveryDateCalculator.Calculate(lat, lon, priority, _parsedOrders);

                    _parsedOrders.Add(new Order
                    {
                        OrderNumber    = $"ORD-CSV-{DateTime.Now:yyyyMMdd}-{idx:D3}",
                        ClientName     = c[0].Trim(),
                        ClientPhone    = c.Length > 1 ? c[1].Trim() : null,
                        Address        = c.Length > 2 ? c[2].Trim() : "",
                        Latitude       = lat,
                        Longitude      = lon,
                        Weight         = weight,
                        Volume         = volume,
                        DeliveryPrice  = price,
                        CashOnDelivery = cod,
                        Priority       = priority,
                        Notes          = c.Length > 10 ? c[10].Trim() : "Импорт CSV",
                        DeliveryDate   = date,
                        Status         = OrderStatus.New
                    });
                }
                catch (Exception ex) { errors.Add($"Строка {i + 1}: {ex.Message}"); }
            }

            // Предпросмотр
            PreviewHeader.Text       = $"📋 Предпросмотр — {_parsedOrders.Count} заказов готово к импорту:";
            PreviewHeader.Visibility = Visibility.Visible;
            PreviewGrid.ItemsSource  = _parsedOrders.Select(o => new
            {
                Клиент    = o.ClientName,
                Адрес     = o.Address,
                Вес       = $"{o.Weight} кг",
                Цена      = $"{o.DeliveryPrice:N0} ₽",
                Дата      = o.DeliveryDate?.ToString("dd.MM.yyyy"),
                Приоритет = o.PriorityDisplay
            }).ToList();
            PreviewGrid.Visibility  = Visibility.Visible;

            var msg = $"✅ Готово к импорту: {_parsedOrders.Count} заказов";
            if (errors.Any())
                msg += $"\n⚠️ Пропущено строк: {errors.Count}\n" + string.Join("\n", errors.Take(5));
            ShowResult(msg, _parsedOrders.Count > 0);
            ImportBtn.IsEnabled = _parsedOrders.Count > 0;
        }
        catch (Exception ex)
        {
            ShowResult($"❌ Ошибка чтения файла: {ex.Message}", false);
        }
    }

    // ─── Импорт в БД ─────────────────────────────────────────────────────────────
    private async void Import_Click(object sender, RoutedEventArgs e)
    {
        ImportBtn.IsEnabled = false;
        try
        {
            using var scope = App.Services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IOrderRepository>();

            int n = 0;
            foreach (var order in _parsedOrders)
            {
                order.OrderNumber = $"ORD-{DateTime.Now:yyyyMMddHHmmss}-{++n:D3}";
                await repo.AddAsync(order);
            }
            await repo.SaveChangesAsync();

            ImportedCount = _parsedOrders.Count;
            ShowResult($"🎉 Успешно импортировано {ImportedCount} заказов! Они появились в разделе «Заказы».", true);
            DialogResult = true;
        }
        catch (Exception ex)
        {
            ShowResult($"❌ Ошибка импорта: {ex.Message}", false);
            ImportBtn.IsEnabled = true;
        }
    }

    // ─── Скачать шаблон ──────────────────────────────────────────────────────────
    private void DownloadTemplate_Click(object sender, RoutedEventArgs e)
    {
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            "шаблон_импорта_заказов.csv");

        var sb = new StringBuilder();
        sb.AppendLine("ClientName;ClientPhone;Address;Latitude;Longitude;Weight;Volume;DeliveryPrice;CashOnDelivery;Priority;Notes");
        sb.AppendLine("ООО Альфа;+7(495)100-10-01;г. Москва ул. Тверская д. 1;55.7558;37.6173;50;0.5;1500;0;Normal;Хрупкое");
        sb.AppendLine("ИП Смирнов А.В.;+7(495)200-20-02;г. Москва Ленинградский пр-т д. 80;55.8094;37.5119;120;1.2;2800;0;High;");
        sb.AppendLine("ЗАО Бета;+7(495)300-30-03;г. Москва ул. Арбат д. 25;55.7494;37.5925;30;0.3;1200;0;Urgent;Срочная доставка");
        sb.AppendLine("ООО Гамма;;г. Подольск пр-т Ленина д. 100;55.4280;37.5440;200;2.0;3500;500;Low;Тяжёлый груз");

        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        MessageBox.Show($"✅ Шаблон сохранён на Рабочий стол:\n{path}",
            "Шаблон готов", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ShowResult(string msg, bool success)
    {
        ResultText.Text = msg;
        ResultBorder.Background = new SolidColorBrush(success
            ? Color.FromRgb(240, 253, 244)
            : Color.FromRgb(254, 242, 242));
        ResultBorder.Visibility = Visibility.Visible;
    }
}
