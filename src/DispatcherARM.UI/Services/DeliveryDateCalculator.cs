using DispatcherARM.Domain.Entities;
using DispatcherARM.Domain.Enums;

namespace DispatcherARM.UI.Services;

/// <summary>
/// Рассчитывает дату доставки по расстоянию, приоритету и загруженности
/// </summary>
public static class DeliveryDateCalculator
{

    public static DateTime Calculate(
        double lat, double lon,
        OrderPriority priority,
        IEnumerable<Order> existingOrders)
    {
        // ── P1-2 FIX: координаты депо читаются из настроек ────────────────────
        double depotLat = AppSettings.Instance.DepotLat;
        double depotLon = AppSettings.Instance.DepotLon;
        double km = HaversineKm(depotLat, depotLon, lat, lon);

        // Базовое смещение по расстоянию
        int days = km switch
        {
            <= 30  => 0,
            <= 100 => 1,
            <= 300 => 2,
            _      => 3
        };

        // Корректировка по приоритету
        int adj = priority switch
        {
            OrderPriority.Urgent => 0,
            OrderPriority.High   => Math.Max(0, days - 1),
            OrderPriority.Normal => days,
            OrderPriority.Low    => days + 1,
            _                    => days
        };

        var date = DateTime.Today.AddDays(adj);

        // Если на эту дату уже ≥ 3 активных заказов — сдвинуть
        var busy = existingOrders.Count(o =>
            o.DeliveryDate.HasValue &&
            o.DeliveryDate.Value.Date == date.Date &&
            o.Status is not OrderStatus.Delivered and not OrderStatus.Cancelled);

        if (busy >= 3) date = date.AddDays(1);
        if (date < DateTime.Today) date = DateTime.Today;

        return date;
    }

    /// <summary>Расстояние по формуле Haversine (км)</summary>
    public static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371.0;
        double dLat = (lat2 - lat1) * Math.PI / 180.0;
        double dLon = (lon2 - lon1) * Math.PI / 180.0;
        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                   Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0) *
                   Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }
}
