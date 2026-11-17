namespace DispatcherARM.Domain.Enums;

/// <summary>
/// Статус транспортного средства
/// </summary>
public enum VehicleStatus
{
    /// <summary>Доступен</summary>
    Available = 0,

    /// <summary>На маршруте</summary>
    OnRoute = 1,

    /// <summary>На обслуживании</summary>
    Maintenance = 2,

    /// <summary>Недоступен</summary>
    Unavailable = 3
}
