using DispatcherARM.Domain.Enums;

namespace DispatcherARM.Domain.Entities;

/// <summary>
/// Заказ на доставку
/// </summary>
public class Order : BaseEntity
{
    /// <summary>Номер заказа (автогенерация)</summary>
    public required string OrderNumber { get; set; }

    /// <summary>Внешний ID из 1С (если заказ пришёл из 1С)</summary>
    public string? ExternalId { get; set; }

    /// <summary>Источник заказа (Ручной / CSV / Веб / 1С)</summary>
    public OrderSource Source { get; set; } = OrderSource.Manual;

    /// <summary>Имя клиента</summary>
    public required string ClientName { get; set; }

    /// <summary>Телефон клиента</summary>
    public string? ClientPhone { get; set; }

    /// <summary>Адрес доставки</summary>
    public required string Address { get; set; }

    /// <summary>Широта</summary>
    public double Latitude { get; set; }

    /// <summary>Долгота</summary>
    public double Longitude { get; set; }

    /// <summary>Вес груза (кг)</summary>
    public double Weight { get; set; }

    /// <summary>Объём груза (м³)</summary>
    public double Volume { get; set; }

    /// <summary>Начало временного окна доставки</summary>
    public TimeSpan? TimeWindowStart { get; set; }

    /// <summary>Конец временного окна доставки</summary>
    public TimeSpan? TimeWindowEnd { get; set; }

    /// <summary>Статус заказа</summary>
    public OrderStatus Status { get; set; } = OrderStatus.New;

    /// <summary>Приоритет</summary>
    public OrderPriority Priority { get; set; } = OrderPriority.Normal;

    /// <summary>Примечания</summary>
    public string? Notes { get; set; }

    /// <summary>Планируемая дата доставки</summary>
    public DateTime? DeliveryDate { get; set; }

    /// <summary>Фактическая дата завершения доставки</summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>Дата и время начала погрузки</summary>
    public DateTime? LoadingStartedAt { get; set; }

    // ====== ФИНАНСЫ ======

    /// <summary>Стоимость доставки (руб) — цена для клиента</summary>
    public decimal DeliveryPrice { get; set; } = 0;

    /// <summary>Стоимость товара (руб)</summary>
    public decimal GoodsValue { get; set; } = 0;

    /// <summary>Наложенный платёж (руб, 0 = безналичная оплата)</summary>
    public decimal CashOnDelivery { get; set; } = 0;

    /// <summary>Оплата получена</summary>
    public bool IsPaid { get; set; } = false;

    // Навигационные свойства

    /// <summary>Водитель, которому назначен заказ</summary>
    public int? AssignedDriverId { get; set; }
    public virtual Driver? AssignedDriver { get; set; }

    /// <summary>Точка маршрута, к которой привязан заказ</summary>
    public virtual Waypoint? Waypoint { get; set; }

    /// <summary>Позиции заказа (товары/продукция)</summary>
    public virtual ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();

    // ── Вычисляемые свойства для отображения в UI ──
    public string StatusDisplay => Status switch
    {
        OrderStatus.New       => "📦 Новый",
        OrderStatus.Assigned  => "👤 Назначен",
        OrderStatus.Loading   => "📦 Погрузка",
        OrderStatus.InTransit => CompletedAt.HasValue ? "🕐 Ожидает подтв." : "🚗 В пути",
        OrderStatus.Delivered => "✅ Выполнен",
        OrderStatus.Cancelled => "❌ Отменён",
        _                     => Status.ToString()
    };

    public string PriorityDisplay => Priority switch
    {
        OrderPriority.Urgent => "🔴 Срочный",
        OrderPriority.High   => "🟠 Высокий",
        OrderPriority.Normal => "🟡 Обычный",
        OrderPriority.Low    => "🟢 Низкий",
        _                    => Priority.ToString()
    };

    public string SourceDisplay => Source switch
    {
        OrderSource.Manual  => "✍️ Ручной",
        OrderSource.Csv     => "📄 CSV",
        OrderSource.Web     => "🌐 Веб",
        OrderSource.OneC    => "🔗 1С",
        _                   => Source.ToString()
    };
}
