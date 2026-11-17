using DispatcherARM.Domain.Enums;

namespace DispatcherARM.Domain.Entities;

/// <summary>
/// Водитель
/// </summary>
public class Driver : BaseEntity
{
    /// <summary>Полное имя водителя</summary>
    public required string FullName { get; set; }

    /// <summary>Номер телефона</summary>
    public required string Phone { get; set; }

    /// <summary>Номер водительского удостоверения</summary>
    public required string LicenseNumber { get; set; }

    /// <summary>Дата рождения</summary>
    public DateTime? BirthDate { get; set; }

    /// <summary>Email</summary>
    public string? Email { get; set; }

    /// <summary>Адрес проживания</summary>
    public string? Address { get; set; }

    /// <summary>Категория ВУ (B, C, D, E)</summary>
    public string? LicenseCategory { get; set; } = "B,C";

    /// <summary>Дата окончания срока действия ВУ</summary>
    public DateTime? LicenseExpiryDate { get; set; }

    /// <summary>Оклад (руб/мес)</summary>
    public decimal BaseSalary { get; set; } = 45000;

    /// <summary>Ставка за км (руб)</summary>
    public decimal RatePerKm { get; set; } = 0;

    /// <summary>Ставка за точку доставки (руб)</summary>
    public decimal RatePerDelivery { get; set; } = 150;

    /// <summary>График работы (описание: 5/2, 2/2, сменный и т.д.)</summary>
    public string? WorkSchedule { get; set; } = "5/2";

    /// <summary>Дата начала работы в компании</summary>
    public DateTime? HireDate { get; set; }

    /// <summary>Примечания</summary>
    public string? Notes { get; set; }

    /// <summary>Текущий статус водителя</summary>
    public DriverStatus Status { get; set; } = DriverStatus.Available;

    /// <summary>ID назначенного транспортного средства</summary>
    public int? AssignedVehicleId { get; set; }

    /// <summary>ID связанного пользователя</summary>
    public int? UserId { get; set; }

    // Навигационные свойства

    /// <summary>Назначенное транспортное средство</summary>
    public virtual Vehicle? AssignedVehicle { get; set; }

    /// <summary>Связанный пользователь</summary>
    public virtual User? User { get; set; }

    /// <summary>Маршруты водителя</summary>
    public virtual ICollection<Route> Routes { get; set; } = new List<Route>();
}
