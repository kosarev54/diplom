namespace DispatcherARM.Domain.Enums;

/// <summary>
/// Статус маршрута
/// </summary>
public enum RouteStatus
{
    /// <summary>Черновик</summary>
    Draft = 0,

    /// <summary>Запланирован</summary>
    Planned = 1,

    /// <summary>В процессе выполнения</summary>
    InProgress = 2,

    /// <summary>Завершён</summary>
    Completed = 3,

    /// <summary>Отменён</summary>
    Cancelled = 4
}
