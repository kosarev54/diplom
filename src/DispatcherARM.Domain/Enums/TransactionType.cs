namespace DispatcherARM.Domain.Enums;

/// <summary>
/// Тип финансовой операции
/// </summary>
public enum TransactionType
{
    /// <summary>Начисление за выполненный заказ</summary>
    Earning,
    /// <summary>Вывод средств на карту</summary>
    Withdrawal,
    /// <summary>Бонус от компании</summary>
    Bonus,
    /// <summary>Штраф</summary>
    Penalty
}
