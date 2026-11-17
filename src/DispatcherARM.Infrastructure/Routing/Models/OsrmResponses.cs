using System.Text.Json.Serialization;

namespace DispatcherARM.Infrastructure.Routing.Models;

/// <summary>
/// Ответ OSRM Route API
/// </summary>
public class OsrmRouteResponse
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("routes")]
    public List<OsrmRoute> Routes { get; set; } = new();

    [JsonPropertyName("waypoints")]
    public List<OsrmWaypoint> Waypoints { get; set; } = new();
}

/// <summary>
/// Маршрут OSRM
/// </summary>
public class OsrmRoute
{
    /// <summary>Дистанция в метрах</summary>
    [JsonPropertyName("distance")]
    public double Distance { get; set; }

    /// <summary>Время в секундах</summary>
    [JsonPropertyName("duration")]
    public double Duration { get; set; }

    /// <summary>Геометрия маршрута (encoded polyline)</summary>
    [JsonPropertyName("geometry")]
    public string Geometry { get; set; } = string.Empty;

    [JsonPropertyName("legs")]
    public List<OsrmLeg> Legs { get; set; } = new();
}

/// <summary>
/// Отрезок маршрута
/// </summary>
public class OsrmLeg
{
    [JsonPropertyName("distance")]
    public double Distance { get; set; }

    [JsonPropertyName("duration")]
    public double Duration { get; set; }

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;
}

/// <summary>
/// Точка привязки OSRM
/// </summary>
public class OsrmWaypoint
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Координаты [longitude, latitude]</summary>
    [JsonPropertyName("location")]
    public double[] Location { get; set; } = Array.Empty<double>();

    [JsonPropertyName("distance")]
    public double Distance { get; set; }

    [JsonPropertyName("waypoint_index")]
    public int? WaypointIndex { get; set; }

    [JsonPropertyName("trips_index")]
    public int? TripsIndex { get; set; }
}

/// <summary>
/// Ответ OSRM Table API (матрица расстояний)
/// </summary>
public class OsrmTableResponse
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    /// <summary>Матрица длительностей (секунды), null = нет маршрута</summary>
    [JsonPropertyName("durations")]
    public double?[][]? Durations { get; set; }

    /// <summary>Матрица расстояний (метры)</summary>
    [JsonPropertyName("distances")]
    public double?[][]? Distances { get; set; }

    [JsonPropertyName("sources")]
    public List<OsrmWaypoint> Sources { get; set; } = new();

    [JsonPropertyName("destinations")]
    public List<OsrmWaypoint> Destinations { get; set; } = new();
}

/// <summary>
/// Ответ OSRM Trip API (оптимизированный маршрут)
/// </summary>
public class OsrmTripResponse
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("trips")]
    public List<OsrmRoute> Trips { get; set; } = new();

    [JsonPropertyName("waypoints")]
    public List<OsrmWaypoint> Waypoints { get; set; } = new();
}

/// <summary>
/// Ответ OSRM Nearest API
/// </summary>
public class OsrmNearestResponse
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("waypoints")]
    public List<OsrmWaypoint> Waypoints { get; set; } = new();
}
