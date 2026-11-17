namespace DispatcherARM.Domain.Enums;

/// <summary>
/// Роль пользователя в системе
/// </summary>
public enum UserRole
{
    /// <summary>Администратор системы</summary>
    Admin = 0,

    /// <summary>Диспетчер</summary>
    Dispatcher = 1,

    /// <summary>Водитель</summary>
    Driver = 2
}
