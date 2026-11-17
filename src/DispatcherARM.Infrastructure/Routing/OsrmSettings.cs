namespace DispatcherARM.Infrastructure.Routing;

/// <summary>
/// Настройки подключения к OSRM-серверу
/// </summary>
public class OsrmSettings
{
    /// <summary>URL публичного OSRM-сервера</summary>
    public string PublicServerUrl { get; set; } = "https://router.project-osrm.org";

    /// <summary>URL локального OSRM-сервера</summary>
    public string LocalServerUrl { get; set; } = "http://localhost:5000";

    /// <summary>Использовать локальный сервер</summary>
    public bool UseLocalServer { get; set; } = false;

    /// <summary>Таймаут запроса (секунды)</summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>Профиль маршрутизации (driving, walking, cycling)</summary>
    public string Profile { get; set; } = "driving";

    /// <summary>Получить активный URL сервера</summary>
    public string GetActiveUrl() => UseLocalServer ? LocalServerUrl : PublicServerUrl;
}
