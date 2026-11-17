using System.Globalization;
using System.Net.Http.Json;
using DispatcherARM.Infrastructure.Routing.Models;

namespace DispatcherARM.Infrastructure.Routing;

/// <summary>
/// HTTP-клиент для OSRM API
/// </summary>
public class OsrmClient
{
    private readonly HttpClient _httpClient;
    private readonly OsrmSettings _settings;

    public OsrmClient(HttpClient httpClient, OsrmSettings settings)
    {
        _httpClient = httpClient;
        _settings = settings;
        _httpClient.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds);
    }

    /// <summary>
    /// Построить маршрут через указанные точки
    /// </summary>
    /// <param name="coordinates">Список координат (широта, долгота)</param>
    /// <returns>Ответ OSRM с маршрутом</returns>
    public async Task<OsrmRouteResponse?> GetRouteAsync(List<(double Lat, double Lon)> coordinates)
    {
        var coordString = FormatCoordinates(coordinates);
        var url = $"{_settings.GetActiveUrl()}/route/v1/{_settings.Profile}/{coordString}?overview=full&geometries=polyline&steps=false";

        try
        {
            return await _httpClient.GetFromJsonAsync<OsrmRouteResponse>(url);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[OSRM] Ошибка Route API: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Получить матрицу расстояний и времени между точками
    /// </summary>
    /// <param name="coordinates">Список координат</param>
    /// <returns>Матрица расстояний/времени</returns>
    public async Task<OsrmTableResponse?> GetTableAsync(List<(double Lat, double Lon)> coordinates)
    {
        var coordString = FormatCoordinates(coordinates);
        var url = $"{_settings.GetActiveUrl()}/table/v1/{_settings.Profile}/{coordString}?annotations=duration,distance";

        try
        {
            return await _httpClient.GetFromJsonAsync<OsrmTableResponse>(url);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[OSRM] Ошибка Table API: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Получить оптимизированный маршрут (решение TSP)
    /// </summary>
    /// <param name="coordinates">Список координат</param>
    /// <param name="roundtrip">Возврат в начальную точку</param>
    /// <returns>Оптимизированный маршрут</returns>
    public async Task<OsrmTripResponse?> GetTripAsync(List<(double Lat, double Lon)> coordinates, bool roundtrip = true)
    {
        var coordString = FormatCoordinates(coordinates);
        var roundtripParam = roundtrip ? "true" : "false";
        var url = $"{_settings.GetActiveUrl()}/trip/v1/{_settings.Profile}/{coordString}?overview=full&geometries=polyline&roundtrip={roundtripParam}&source=first";

        try
        {
            return await _httpClient.GetFromJsonAsync<OsrmTripResponse>(url);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[OSRM] Ошибка Trip API: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Привязать координату к ближайшей точке дорожной сети
    /// </summary>
    public async Task<OsrmNearestResponse?> GetNearestAsync(double lat, double lon)
    {
        var coord = FormatCoordinate(lat, lon);
        var url = $"{_settings.GetActiveUrl()}/nearest/v1/{_settings.Profile}/{coord}?number=1";

        try
        {
            return await _httpClient.GetFromJsonAsync<OsrmNearestResponse>(url);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[OSRM] Ошибка Nearest API: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Форматировать список координат в строку OSRM (lon,lat;lon,lat;...)
    /// Важно: OSRM принимает координаты в формате longitude,latitude!
    /// </summary>
    private static string FormatCoordinates(List<(double Lat, double Lon)> coordinates)
    {
        return string.Join(";", coordinates.Select(c => FormatCoordinate(c.Lat, c.Lon)));
    }

    /// <summary>
    /// Форматировать одну координату (lon,lat)
    /// </summary>
    private static string FormatCoordinate(double lat, double lon)
    {
        return $"{lon.ToString(CultureInfo.InvariantCulture)},{lat.ToString(CultureInfo.InvariantCulture)}";
    }
}
