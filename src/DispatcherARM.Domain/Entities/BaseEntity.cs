namespace DispatcherARM.Domain.Entities;

/// <summary>
/// Базовая сущность с общими полями
/// </summary>
public abstract class BaseEntity
{
    /// <summary>Уникальный идентификатор</summary>
    public int Id { get; set; }

    /// <summary>Дата и время создания</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Дата и время последнего обновления</summary>
    public DateTime? UpdatedAt { get; set; }
}
