using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml;
using System.Xml.Linq;
using DispatcherARM.Domain.Entities;
using DispatcherARM.Domain.Enums;
using DispatcherARM.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace DispatcherARM.UI.Services;

// ── DTO для обмена с 1С (JSON-формат) ──────────────────────────────────────

public class OneCOrdersFile
{
    public string Source        { get; set; } = "1C:Enterprise";
    public string Version       { get; set; } = "8.3";
    public DateTime ExportDate  { get; set; } = DateTime.Now;
    public List<OneCOrder> Orders { get; set; } = new();
}

public class OneCOrder
{
    public string   ExternalId    { get; set; } = "";
    public string   ClientName    { get; set; } = "";
    public string?  ClientPhone   { get; set; }
    public string   Address       { get; set; } = "";
    public double   Latitude      { get; set; }
    public double   Longitude     { get; set; }
    public double   Weight        { get; set; }
    public double   Volume        { get; set; }
    public decimal  DeliveryPrice { get; set; }
    public string   Priority      { get; set; } = "Normal";
    public string?  DeliveryDate  { get; set; }
    public string?  Notes         { get; set; }
    public List<OneCGood> Goods   { get; set; } = new();
}

public class OneCGood
{
    public string  Name          { get; set; } = "";
    public double  Qty           { get; set; }
    public string  Unit          { get; set; } = "шт";
    public decimal UnitPrice     { get; set; }
    public string? ArticleNumber { get; set; }
}

public class OneCStatusesFile
{
    public DateTime ExportDate { get; set; } = DateTime.Now;
    public List<OneCStatus> Statuses { get; set; } = new();
}

public class OneCStatus
{
    public string?   ExternalId   { get; set; }
    public int       InternalId   { get; set; }
    public string    Status       { get; set; } = "";
    public string?   DriverName   { get; set; }
    public string?   Vehicle      { get; set; }
    public DateTime? DeliveredAt  { get; set; }
    public decimal   Amount       { get; set; }
}

// ── Сервис ──────────────────────────────────────────────────────────────────

/// <summary>
/// Сервис интеграции с 1С:Предприятие через файловый обмен (JSON + XML)
/// </summary>
public class OneCIntegrationService : IDisposable
{
    private readonly IServiceProvider  _services;
    private FileSystemWatcher?         _watcher;
    private bool                       _watching;
    private string                     _exchangeFolder = "";

    /// <summary>Событие: импортировано N заказов</summary>
    public event Action<int, string>? OrdersImported;
    /// <summary>Событие: экспортированы статусы</summary>
    public event Action<int, string>? StatusesExported;
    /// <summary>Событие: ошибка</summary>
    public event Action<string>?      ErrorOccurred;

    public bool IsWatching => _watching;
    public string ExchangeFolder => _exchangeFolder;

    public OneCIntegrationService(IServiceProvider services)
    {
        _services = services;
    }

    // ── Авто-мониторинг папки ─────────────────────────────────────────────────

    public void StartWatcher(string folder)
    {
        if (_watching) return;
        if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

        _exchangeFolder = folder;

        // ── БАГ 3 FIX: отдельная папка для обработанных файлов ────────────────
        // Фильтр НЕ матчит processed/ подпапку (Created не срабатывает на файлы в подпапке)
        var processedDir = Path.Combine(folder, "processed");
        Directory.CreateDirectory(processedDir);

        _watcher = new FileSystemWatcher(folder)
        {
            Filter              = "orders_from_1c*.*",
            NotifyFilter        = NotifyFilters.FileName,
            EnableRaisingEvents = true
        };
        // ── Только Created, не Changed — иначе двойная обработка ──────────────
        _watcher.Created += OnFileDetected;
        _watching = true;
    }

    public void StopWatcher()
    {
        if (_watcher != null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
            _watcher = null;
        }
        _watching = false;
    }

    // ── Защита от параллельной обработки одного файла ─────────────────────────
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte> _processingFiles
        = new();

    private async void OnFileDetected(object sender, FileSystemEventArgs e)
    {
        // Если файл уже обрабатывается — пропускаем (TryAdd вернёт false)
        if (!_processingFiles.TryAdd(e.FullPath, 0)) return;

        await Task.Delay(800); // Ждём завершения записи
        try
        {
            var ext = Path.GetExtension(e.FullPath).ToLowerInvariant();
            (int count, string msg) = ext == ".xml"
                ? await ImportFromXmlAsync(e.FullPath)
                : await ImportFromFileAsync(e.FullPath);

            OrdersImported?.Invoke(count, msg);

            // ── Перемещаем в processed/ (не в ту же папку!) ──────────────────
            var processedDir = Path.Combine(_exchangeFolder, "processed");
            var destName     = $"{Path.GetFileNameWithoutExtension(e.FullPath)}_done_{DateTime.Now:yyyyMMddHHmmss}{ext}";
            var destPath     = Path.Combine(processedDir, destName);
            File.Move(e.FullPath, destPath, overwrite: true);
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke($"Ошибка файла {e.Name}: {ex.Message}");
        }
        finally
        {
            _processingFiles.TryRemove(e.FullPath, out _);
        }
    }

    // ── Импорт из 1С (JSON) ───────────────────────────────────────────────────

    public async Task<(int Count, string Message)> ImportFromFileAsync(string filePath)
    {
        if (!File.Exists(filePath)) return (0, $"Файл не найден: {filePath}");

        var json    = await File.ReadAllTextAsync(filePath);
        var opts    = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var payload = JsonSerializer.Deserialize<OneCOrdersFile>(json, opts)
            ?? throw new Exception("Не удалось разобрать JSON из 1С");

        return await ImportOrdersAsync(payload.Orders);
    }

    // ── Импорт из 1С (XML) ────────────────────────────────────────────────────

    public async Task<(int Count, string Message)> ImportFromXmlAsync(string filePath)
    {
        if (!File.Exists(filePath)) return (0, $"Файл не найден: {filePath}");

        var xml = XDocument.Load(filePath);
        var ns  = xml.Root?.Name.Namespace ?? XNamespace.None;

        var orders = xml.Descendants(ns + "Заказ")
            .Select(x => new OneCOrder
            {
                ExternalId    = x.Element(ns + "Идентификатор")?.Value ?? "",
                ClientName    = x.Element(ns + "Клиент")?.Element(ns + "Наименование")?.Value ?? "",
                ClientPhone   = x.Element(ns + "Клиент")?.Element(ns + "Телефон")?.Value,
                Address       = x.Element(ns + "АдресДоставки")?.Value ?? "",
                Latitude      = double.TryParse(x.Element(ns + "Широта")?.Value, 
                                    System.Globalization.NumberStyles.Any,
                                    System.Globalization.CultureInfo.InvariantCulture, out var lat) ? lat : 0,
                Longitude     = double.TryParse(x.Element(ns + "Долгота")?.Value,
                                    System.Globalization.NumberStyles.Any,
                                    System.Globalization.CultureInfo.InvariantCulture, out var lon) ? lon : 0,
                Weight        = double.TryParse(x.Element(ns + "ВесКг")?.Value,
                                    System.Globalization.NumberStyles.Any,
                                    System.Globalization.CultureInfo.InvariantCulture, out var w) ? w : 0,
                Volume        = double.TryParse(x.Element(ns + "ОбъемМ3")?.Value,
                                    System.Globalization.NumberStyles.Any,
                                    System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0,
                DeliveryPrice = decimal.TryParse(x.Element(ns + "СтоимостьДоставки")?.Value,
                                    System.Globalization.NumberStyles.Any,
                                    System.Globalization.CultureInfo.InvariantCulture, out var dp) ? dp : 0,
                Priority      = x.Element(ns + "Приоритет")?.Value ?? "Normal",
                DeliveryDate  = x.Element(ns + "ДатаДоставки")?.Value,
                Notes         = x.Element(ns + "Примечание")?.Value,
                Goods         = x.Element(ns + "Товары")?.Elements(ns + "Товар")
                    .Select(g => new OneCGood
                    {
                        Name          = g.Element(ns + "Наименование")?.Value ?? "",
                        Qty           = double.TryParse(g.Element(ns + "Количество")?.Value,
                                            System.Globalization.NumberStyles.Any,
                                            System.Globalization.CultureInfo.InvariantCulture, out var q) ? q : 0,
                        Unit          = g.Element(ns + "ЕдиницаИзмерения")?.Value ?? "шт",
                        UnitPrice     = decimal.TryParse(g.Element(ns + "Цена")?.Value,
                                            System.Globalization.NumberStyles.Any,
                                            System.Globalization.CultureInfo.InvariantCulture, out var up) ? up : 0,
                        ArticleNumber = g.Element(ns + "Артикул")?.Value
                    }).ToList() ?? new()
            }).ToList();

        return await ImportOrdersAsync(orders);
    }

    // ── Общая логика импорта ──────────────────────────────────────────────────

    private async Task<(int Count, string Message)> ImportOrdersAsync(List<OneCOrder> oneCOrders)
    {
        if (!oneCOrders.Any()) return (0, "Файл не содержит заказов");

        using var scope  = _services.CreateScope();
        var repo         = scope.ServiceProvider.GetRequiredService<IOrderRepository>();
        var allOrders    = (await repo.GetAllAsync()).ToList();
        var existingEIds = allOrders
            .Where(o => o.ExternalId != null)
            .Select(o => o.ExternalId!)
            .ToHashSet();

        int imported = 0;
        var skipped  = new List<string>();

        foreach (var oc in oneCOrders)
        {
            // ── Баг FIX: пустой ExternalId — не проверяем на дубликат, генерируем новый ──
            var extId = string.IsNullOrWhiteSpace(oc.ExternalId)
                ? $"AUTO-{Guid.NewGuid():N}"
                : oc.ExternalId;

            if (!string.IsNullOrWhiteSpace(oc.ExternalId) && existingEIds.Contains(extId))
            { skipped.Add(extId); continue; }

            var priority = Enum.TryParse<OrderPriority>(oc.Priority, true, out var p)
                ? p : OrderPriority.Normal;

            double lat = oc.Latitude, lon = oc.Longitude;
            if (lat == 0 && lon == 0) { lat = AppSettings.Instance.DepotLat; lon = AppSettings.Instance.DepotLon; }

            DateTime? delivDate = DateTime.TryParse(oc.DeliveryDate, out var d) ? d
                : DeliveryDateCalculator.Calculate(lat, lon, priority, allOrders);

            // Уникальный порядковый номер с timestamp + счётчик
            var orderNum = $"ORD-1C-{DateTime.Now:yyyyMMddHHmmss}-{imported + 1:D3}";

            var order = new Order
            {
                OrderNumber   = orderNum,
                ExternalId    = extId,
                Source        = OrderSource.OneC,
                ClientName    = oc.ClientName,
                ClientPhone   = oc.ClientPhone,
                Address       = oc.Address,
                Latitude      = lat,
                Longitude     = lon,
                Weight        = oc.Weight,
                Volume        = oc.Volume,
                DeliveryPrice = oc.DeliveryPrice,
                Priority      = priority,
                DeliveryDate  = delivDate,
                Notes         = oc.Notes ?? "Из 1С",
                Status        = OrderStatus.New,
                Items         = oc.Goods.Select(g => new OrderItem
                {
                    Name          = g.Name,
                    Quantity      = g.Qty,
                    Unit          = g.Unit,
                    UnitPrice     = g.UnitPrice,
                    ArticleNumber = g.ArticleNumber
                }).ToList()
            };

            await repo.AddAsync(order);
            allOrders.Add(order);
            existingEIds.Add(oc.ExternalId);
            imported++;
        }

        await repo.SaveChangesAsync();

        var msg = $"Импортировано {imported} заказов из 1С";
        if (skipped.Any()) msg += $", пропущено (дубликаты): {skipped.Count}";
        return (imported, msg);
    }

    // ── Экспорт статусов в 1С (JSON + XML) ───────────────────────────────────

    public async Task<(int Count, string Message)> ExportStatusesAsync(string? folderOverride = null)
    {
        var folder = folderOverride ?? _exchangeFolder;
        if (string.IsNullOrWhiteSpace(folder)) return (0, "Папка обмена не настроена");
        if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

        using var scope  = _services.CreateScope();
        var orderRepo    = scope.ServiceProvider.GetRequiredService<IOrderRepository>();
        var driverRepo   = scope.ServiceProvider.GetRequiredService<IDriverRepository>();

        var orders  = (await orderRepo.GetAllAsync()).Where(o => o.ExternalId != null).ToList();
        var drivers = (await driverRepo.GetAllAsync()).ToList();

        var statuses = orders.Select(o =>
        {
            var drv = drivers.FirstOrDefault(d => d.Id == o.AssignedDriverId);
            return new OneCStatus
            {
                ExternalId  = o.ExternalId,
                InternalId  = o.Id,
                Status      = o.Status.ToString(),
                DriverName  = drv?.FullName,
                Vehicle     = drv?.AssignedVehicle?.LicensePlate,
                DeliveredAt = o.Status == OrderStatus.Delivered ? o.CompletedAt : null,
                Amount      = o.DeliveryPrice
            };
        }).ToList();

        var ts = DateTime.Now.ToString("yyyyMMddHHmmss");

        // JSON
        var jsonOpts = new JsonSerializerOptions { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
        var json     = JsonSerializer.Serialize(new OneCStatusesFile { Statuses = statuses }, jsonOpts);
        await File.WriteAllTextAsync(Path.Combine(folder, $"statuses_to_1c_{ts}.json"), json);

        // XML
        var xmlPath = Path.Combine(folder, $"statuses_to_1c_{ts}.xml");
        await ExportStatusesXmlAsync(statuses, xmlPath);

        return (statuses.Count, $"Экспортировано {statuses.Count} статусов → statuses_to_1c_{ts}.json/.xml");
    }

    private static async Task ExportStatusesXmlAsync(List<OneCStatus> statuses, string path)
    {
        var xns = "urn:1c-logistic-arm:statuses:1.0";
        var doc = new XDocument(
            new XDeclaration("1.0", "utf-8", "yes"),
            new XElement(XName.Get("СтатусыДоставки", xns),
                new XAttribute("ВерсияФормата", "1.0"),
                new XAttribute("ДатаФормирования", DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss")),
                new XAttribute("Источник", "АРМ Диспетчера ТЛК"),
                statuses.Select(s =>
                    new XElement(XName.Get("Статус", xns),
                        new XElement(XName.Get("ВнешнийИд", xns), s.ExternalId ?? ""),
                        new XElement(XName.Get("ВнутреннийИд", xns), s.InternalId),
                        new XElement(XName.Get("СостояниеЗаказа", xns), MapStatusToRu(s.Status)),
                        new XElement(XName.Get("КодСостояния", xns), s.Status),
                        s.DriverName != null ? new XElement(XName.Get("Водитель", xns), s.DriverName) : null!,
                        s.Vehicle    != null ? new XElement(XName.Get("ТС", xns), s.Vehicle) : null!,
                        s.DeliveredAt.HasValue
                            ? new XElement(XName.Get("ДатаДоставки", xns), s.DeliveredAt.Value.ToString("yyyy-MM-ddTHH:mm:ss"))
                            : null!,
                        new XElement(XName.Get("Сумма", xns), s.Amount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture))
                    )
                )
            )
        );

        var settings = new XmlWriterSettings { Indent = true, Encoding = Encoding.UTF8, Async = true };
        using var writer = XmlWriter.Create(path, settings);
        await doc.SaveAsync(writer, CancellationToken.None);
    }

    private static string MapStatusToRu(string s) => s switch
    {
        "New"       => "Новый",
        "Assigned"  => "Назначен",
        "Loading"   => "Погрузка",
        "InTransit" => "В пути",
        "Delivered" => "Доставлен",
        "Cancelled" => "Отменён",
        _           => s
    };

    // ── Генерация тестового файла 1С (JSON + XML) ─────────────────────────────

    public static async Task<string> GenerateSampleFileAsync(string folder)
    {
        if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

        var today = DateTime.Today;
        var sample = new OneCOrdersFile
        {
            Orders = new List<OneCOrder>
            {
                new()
                {
                    ExternalId    = $"1C-{today:yyyyMMdd}-001",
                    ClientName    = "ООО «Пятёрочка» — Магазин №47",
                    ClientPhone   = "+7(495)100-10-01",
                    Address       = "г. Москва, ул. Тверская, д. 15",
                    Latitude      = 55.7625,
                    Longitude     = 37.6092,
                    Weight        = 850.5,
                    Volume        = 5.2,
                    DeliveryPrice = 4500,
                    Priority      = "Normal",
                    DeliveryDate  = today.AddDays(1).ToString("yyyy-MM-dd"),
                    Notes         = "Разгрузка с 08:00 до 11:00. Контакт склада: Иванов И.И.",
                    Goods = new()
                    {
                        new() { Name = "Молоко 3.2% 1л",          Qty = 120, Unit = "шт", UnitPrice = 68.50m,  ArticleNumber = "MLK-001" },
                        new() { Name = "Кефир 2.5% 0.9л",          Qty = 80,  Unit = "шт", UnitPrice = 72.00m,  ArticleNumber = "KFR-001" },
                        new() { Name = "Масло сливочное 200г",      Qty = 40,  Unit = "шт", UnitPrice = 185.00m, ArticleNumber = "BTR-001" }
                    }
                },
                new()
                {
                    ExternalId    = $"1C-{today:yyyyMMdd}-002",
                    ClientName    = "АО «Лента» — Гипермаркет №12",
                    ClientPhone   = "+7(495)200-20-02",
                    Address       = "г. Москва, Ленинградский пр-т, д. 80",
                    Latitude      = 55.8094,
                    Longitude     = 37.5119,
                    Weight        = 1200,
                    Volume        = 8.0,
                    DeliveryPrice = 6800,
                    Priority      = "High",
                    DeliveryDate  = today.AddDays(1).ToString("yyyy-MM-dd"),
                    Notes         = "Срочно! Доставить до 10:00. Контакт: +7(916)555-01-02",
                    Goods = new()
                    {
                        new() { Name = "Сметана 20% 400г", Qty = 200, Unit = "шт", UnitPrice = 95.00m },
                        new() { Name = "Творог 9% 200г",   Qty = 150, Unit = "шт", UnitPrice = 88.50m },
                        new() { Name = "Ряженка 4% 1л",    Qty = 100, Unit = "шт", UnitPrice = 76.00m }
                    }
                },
                new()
                {
                    ExternalId    = $"1C-{today:yyyyMMdd}-003",
                    ClientName    = "ИП Сидоров А.В. — Кафе «Уют»",
                    ClientPhone   = "+7(916)300-30-03",
                    Address       = "г. Москва, ул. Арбат, д. 22",
                    Latitude      = 55.7490,
                    Longitude     = 37.5935,
                    Weight        = 320,
                    Volume        = 1.8,
                    DeliveryPrice = 2200,
                    Priority      = "Low",
                    DeliveryDate  = today.AddDays(2).ToString("yyyy-MM-dd"),
                    Goods = new()
                    {
                        new() { Name = "Сливки 33% 1л",       Qty = 48, Unit = "шт", UnitPrice = 145.00m },
                        new() { Name = "Йогурт греческий 250г", Qty = 60, Unit = "шт", UnitPrice = 89.00m }
                    }
                }
            }
        };

        var jsonOpts = new JsonSerializerOptions { WriteIndented = true };
        var json     = JsonSerializer.Serialize(sample, jsonOpts);
        var jsonPath = Path.Combine(folder, "orders_from_1c_sample.json");
        await File.WriteAllTextAsync(jsonPath, json, Encoding.UTF8);

        // Также генерируем XML
        var xmlPath = Path.Combine(folder, "orders_from_1c_sample.xml");
        await GenerateSampleXmlAsync(sample, xmlPath);

        return jsonPath;
    }

    private static async Task GenerateSampleXmlAsync(OneCOrdersFile data, string path)
    {
        var xns = "urn:1c-logistic-arm:orders:1.0";
        var doc = new XDocument(
            new XDeclaration("1.0", "utf-8", "yes"),
            new XElement(XName.Get("ЗаказыНаДоставку", xns),
                new XAttribute("ВерсияФормата", "1.0"),
                new XAttribute("Версия1С", data.Version),
                new XAttribute("ДатаФормирования", data.ExportDate.ToString("yyyy-MM-ddTHH:mm:ss")),
                new XAttribute("Источник", data.Source),
                data.Orders.Select(o =>
                    new XElement(XName.Get("Заказ", xns),
                        new XElement(XName.Get("Идентификатор", xns), o.ExternalId),
                        new XElement(XName.Get("Приоритет", xns), o.Priority),
                        new XElement(XName.Get("ДатаДоставки", xns), o.DeliveryDate ?? ""),
                        new XElement(XName.Get("АдресДоставки", xns), o.Address),
                        new XElement(XName.Get("Широта", xns), o.Latitude.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)),
                        new XElement(XName.Get("Долгота", xns), o.Longitude.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)),
                        new XElement(XName.Get("ВесКг", xns), o.Weight.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)),
                        new XElement(XName.Get("ОбъемМ3", xns), o.Volume.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)),
                        new XElement(XName.Get("СтоимостьДоставки", xns), o.DeliveryPrice.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)),
                        new XElement(XName.Get("Примечание", xns), o.Notes ?? ""),
                        new XElement(XName.Get("Клиент", xns),
                            new XElement(XName.Get("Наименование", xns), o.ClientName),
                            new XElement(XName.Get("Телефон", xns), o.ClientPhone ?? "")
                        ),
                        new XElement(XName.Get("Товары", xns),
                            o.Goods.Select(g =>
                                new XElement(XName.Get("Товар", xns),
                                    new XElement(XName.Get("Артикул", xns), g.ArticleNumber ?? ""),
                                    new XElement(XName.Get("Наименование", xns), g.Name),
                                    new XElement(XName.Get("Количество", xns), g.Qty.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)),
                                    new XElement(XName.Get("ЕдиницаИзмерения", xns), g.Unit),
                                    new XElement(XName.Get("Цена", xns), g.UnitPrice.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)),
                                    new XElement(XName.Get("Сумма", xns), (g.Qty * (double)g.UnitPrice).ToString("F2", System.Globalization.CultureInfo.InvariantCulture))
                                )
                            )
                        )
                    )
                )
            )
        );

        var settings = new XmlWriterSettings { Indent = true, Encoding = Encoding.UTF8, Async = true };
        using var writer = XmlWriter.Create(path, settings);
        await doc.SaveAsync(writer, CancellationToken.None);
    }

    public void Dispose() => StopWatcher();
}
