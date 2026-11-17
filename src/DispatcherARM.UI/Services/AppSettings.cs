using System.IO;
using System.Text.Json;

namespace DispatcherARM.UI.Services;

/// <summary>
/// Настройки приложения — синглтон с персистентностью в JSON
/// Единственный источник правды для всех конфигурируемых параметров
/// </summary>
public class AppSettings
{
    // ── Синглтон ─────────────────────────────────────────────────────────────
    private static AppSettings? _instance;

    /// <summary>Глобальный экземпляр настроек. Всегда инициализирован.</summary>
    public static AppSettings Instance => _instance ??= Load();

    // ── Пути ─────────────────────────────────────────────────────────────────
    public static string AppDataDir { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DispatcherARM");

    private static string SettingsFile => Path.Combine(AppDataDir, "settings.json");

    // ── OSRM ─────────────────────────────────────────────────────────────────
    public string OsrmPublicUrl  { get; set; } = "https://router.project-osrm.org";
    public string OsrmLocalUrl   { get; set; } = "http://localhost:5000";
    public bool   UseLocalOsrm   { get; set; } = false;

    /// <summary>Активный URL OSRM (локальный или публичный)</summary>
    public string GetActiveOsrmUrl() => UseLocalOsrm ? OsrmLocalUrl : OsrmPublicUrl;

    // ── Депо — ЕДИНСТВЕННОЕ МЕСТО в коде ─────────────────────────────────────
    /// <summary>Широта депо (начальная точка всех маршрутов)</summary>
    public double DepotLat     { get; set; } = 55.7558;

    /// <summary>Долгота депо</summary>
    public double DepotLon     { get; set; } = 37.6173;

    /// <summary>Адрес депо для отображения на карте и в документах</summary>
    public string DepotAddress { get; set; } = "г. Москва, ул. Большая Переяславская, д. 10";

    // ── Организация ───────────────────────────────────────────────────────────
    public string OrganizationName  { get; set; } = "ООО «Транспортно-логистическая компания»";
    public string OrganizationInn   { get; set; } = "7701234567";
    public string OrganizationKpp   { get; set; } = "770101001";
    public string OrganizationOgrn  { get; set; } = "1037700123456";
    public string OrganizationAddress { get; set; } = "г. Москва, ул. Большая Переяславская, д. 10";

    // ── Интеграция ────────────────────────────────────────────────────────────
    public string OneCExchangeFolder { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "1С-Обмен");

    public int WebServerPort { get; set; } = 5050;

    // ── Тарифы ───────────────────────────────────────────────────────────────
    /// <summary>Коэффициент от стоимости заказа для начисления водителю (по умолчанию 15%)</summary>
    public double DriverCommissionRate { get; set; } = 0.15;

    /// <summary>Цена топлива (руб/л) для расчёта себестоимости рейса</summary>
    public double FuelPricePerLiter { get; set; } = 63.0;

    // ── Сохранение / загрузка ─────────────────────────────────────────────────
    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsFile)) return new AppSettings();
            var json = File.ReadAllText(SettingsFile, System.Text.Encoding.UTF8);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(AppDataDir);
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(SettingsFile, json, System.Text.Encoding.UTF8);

        // Сбрасываем кэш синглтона, чтобы следующий вызов Instance получил свежие данные
        _instance = this;
    }

    /// <summary>Перезагрузить из файла (например, после ручного редактирования)</summary>
    public static void Reload() => _instance = Load();
}
