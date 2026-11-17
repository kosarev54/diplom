namespace DispatcherARM.Domain.Enums;

/// <summary>
/// Приоритет заказа
/// </summary>
public enum OrderPriority
{
    /// <summary>Низкий</summary>
    Low = 0,

    /// <summary>Обычный</summary>
    Normal = 1,

    /// <summary>Высокий</summary>
    High = 2,

    /// <summary>Срочный</summary>
    Urgent = 3
}
