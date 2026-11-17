namespace DispatcherARM.Domain.Enums;

/// <summary>
/// Статус водителя (независимый от статуса ТС)
/// </summary>
public enum DriverStatus
{
    /// <summary>Свободен, доступен для назначения</summary>
    Available = 0,

    /// <summary>В рейсе (выполняет маршрут)</summary>
    OnRoute = 1,

    /// <summary>В отпуске</summary>
    OnLeave = 2,

    /// <summary>Отстранён (медотвод, дисциплинарные меры и т.д.)</summary>
    Suspended = 3
}
