namespace DispatcherARM.Domain.Entities;

/// <summary>
/// Позиция (товар) в заказе — список продукции, которую везёт водитель
/// </summary>
public class OrderItem : BaseEntity
{
    /// <summary>Ссылка на заказ</summary>
    public int OrderId { get; set; }
    public virtual Order? Order { get; set; }

    /// <summary>Наименование товара / продукции</summary>
    public required string Name { get; set; }

    /// <summary>Количество</summary>
    public double Quantity { get; set; }

    /// <summary>Единица измерения (шт, кг, л, упак.)</summary>
    public string Unit { get; set; } = "шт";

    /// <summary>Цена за единицу (руб)</summary>
    public decimal UnitPrice { get; set; } = 0;

    /// <summary>Итоговая сумма по позиции</summary>
    public decimal TotalPrice => (decimal)Quantity * UnitPrice;

    /// <summary>Артикул / штрих-код (из 1С)</summary>
    public string? ArticleNumber { get; set; }
}
