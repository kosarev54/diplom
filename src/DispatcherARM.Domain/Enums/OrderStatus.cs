namespace DispatcherARM.Domain.Enums;

/// <summary>
/// Статус заказа
/// </summary>
public enum OrderStatus
{
    /// <summary>Новый заказ</summary>
    New = 0,

    /// <summary>Назначен водителю</summary>
    Assigned = 1,

    /// <summary>Погрузка на складе (диспетчер подтвердил загрузку ТС)</summary>
    Loading = 5,

    /// <summary>В пути</summary>
    InTransit = 2,

    /// <summary>Доставлен</summary>
    Delivered = 3,

    /// <summary>Отменён</summary>
    Cancelled = 4
}
