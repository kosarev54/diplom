using DispatcherARM.Domain.Enums;

namespace DispatcherARM.Domain.Entities;

/// <summary>
/// Транспортное средство
/// </summary>
public class Vehicle : BaseEntity
{
    /// <summary>Государственный номер</summary>
    public required string LicensePlate { get; set; }

    /// <summary>Модель ТС</summary>
    public required string Model { get; set; }

    /// <summary>Год выпуска</summary>
    public int? Year { get; set; }

    /// <summary>VIN-номер</summary>
    public string? Vin { get; set; }

    /// <summary>Тип ТС</summary>
    public VehicleType Type { get; set; }

    /// <summary>Максимальная грузоподъёмность (кг)</summary>
    public double MaxWeightKg { get; set; }

    /// <summary>Максимальный объём груза (м³)</summary>
    public double MaxVolumeM3 { get; set; }

    /// <summary>Расход топлива на 100 км (л)</summary>
    public double FuelConsumptionPer100Km { get; set; }

    /// <summary>Текущий пробег (км)</summary>
    public double OdometerKm { get; set; }

    /// <summary>Дата последнего технического обслуживания</summary>
    public DateTime? LastMaintenanceDate { get; set; }

    /// <summary>Пробег на момент последнего ТО (км)</summary>
    public double? LastMaintenanceOdometerKm { get; set; }

    /// <summary>Следующее ТО через (км)</summary>
    public double? NextMaintenanceKm { get; set; } = 10000;

    /// <summary>Дата следующего ТО (плановая)</summary>
    public DateTime? NextMaintenanceDate { get; set; }

    /// <summary>Страховой полис — номер</summary>
    public string? InsurancePolicy { get; set; }

    /// <summary>Дата окончания страховки</summary>
    public DateTime? InsuranceExpiry { get; set; }

    /// <summary>Стоимость аренды/лизинга в месяц (руб)</summary>
    public decimal? MonthlyCost { get; set; }

    /// <summary>Примечания / особенности</summary>
    public string? Notes { get; set; }

    /// <summary>Текущий статус</summary>
    public VehicleStatus Status { get; set; } = VehicleStatus.Available;

    // Навигационные свойства

    /// <summary>Назначенные водители</summary>
    public virtual ICollection<Driver> Drivers { get; set; } = new List<Driver>();
}
