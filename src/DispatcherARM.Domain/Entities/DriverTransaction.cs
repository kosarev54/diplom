using DispatcherARM.Domain.Enums;

namespace DispatcherARM.Domain.Entities;

/// <summary>
/// Финансовая транзакция водителя
/// </summary>
public class DriverTransaction : BaseEntity
{
    /// <summary>ID водителя</summary>
    public int DriverId { get; set; }

    /// <summary>Тип операции</summary>
    public TransactionType Type { get; set; }

    /// <summary>Сумма операции (положительная)</summary>
    public decimal Amount { get; set; }

    /// <summary>Описание операции</summary>
    public string? Description { get; set; }

    /// <summary>ID связанного заказа (если начисление за заказ)</summary>
    public int? OrderId { get; set; }

    /// <summary>ID связанного маршрута</summary>
    public int? RouteId { get; set; }

    /// <summary>Статус: подтверждена / ожидает</summary>
    public bool IsConfirmed { get; set; } = true;

    /// <summary>Реквизиты вывода (номер карты, если вывод)</summary>
    public string? WithdrawalDetails { get; set; }

    // Навигационные свойства
    public virtual Driver? Driver { get; set; }
    public virtual Order?  Order  { get; set; }
    public virtual Route?  Route  { get; set; }
}
