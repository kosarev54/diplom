using DispatcherARM.Domain.Enums;

namespace DispatcherARM.Domain.Entities;

/// <summary>
/// Пользователь системы
/// </summary>
public class User : BaseEntity
{
    /// <summary>Логин</summary>
    public required string Login { get; set; }

    /// <summary>Хэш пароля (BCrypt)</summary>
    public required string PasswordHash { get; set; }

    /// <summary>Роль пользователя</summary>
    public UserRole Role { get; set; }

    /// <summary>Полное имя</summary>
    public required string FullName { get; set; }

    /// <summary>Электронная почта</summary>
    public string? Email { get; set; }

    /// <summary>Активен ли пользователь</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Подтверждён ли аккаунт администратором</summary>
    public bool IsApproved { get; set; } = true;

    /// <summary>Дата регистрации</summary>
    public DateTime RegisteredAt { get; set; } = DateTime.Now;

    /// <summary>Баланс — подтверждённые средства (руб)</summary>
    public decimal Balance { get; set; } = 0;

    /// <summary>Ожидающие начисления (после выполнения заказа, до подтверждения адм.)</summary>
    public decimal PendingBalance { get; set; } = 0;

    /// <summary>Номер карты для вывода средств</summary>
    public string? CardNumber { get; set; }

    /// <summary>Телефон</summary>
    public string? Phone { get; set; }
}
