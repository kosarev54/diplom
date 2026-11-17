using DispatcherARM.Domain.Entities;
using DispatcherARM.Domain.Interfaces;

namespace DispatcherARM.Application.Services;

/// <summary>
/// Сервис аутентификации
/// </summary>
public class AuthService
{
    private readonly IUserRepository _userRepository;

    /// <summary>Текущий авторизованный пользователь</summary>
    public User? CurrentUser { get; private set; }

    /// <summary>Авторизован ли пользователь</summary>
    public bool IsAuthenticated => CurrentUser != null;

    public AuthService(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    /// <summary>
    /// Выполнить вход
    /// </summary>
    /// <param name="login">Логин</param>
    /// <param name="password">Пароль</param>
    /// <returns>Успешность входа</returns>
    public async Task<(bool Success, string Message)> LoginAsync(string login, string password)
    {
        if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(password))
            return (false, "Введите логин и пароль");

        var user = await _userRepository.GetByLoginAsync(login);
        if (user == null)
            return (false, "Пользователь не найден");

        if (!user.IsActive)
            return (false, "Учётная запись деактивирована");

        if (!user.IsApproved)
            return (false, "Аккаунт ожидает подтверждения администратором");

        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            return (false, "Неверный пароль");

        CurrentUser = user;
        return (true, $"Добро пожаловать, {user.FullName}!");
    }

    /// <summary>
    /// Выйти из системы
    /// </summary>
    public void Logout()
    {
        CurrentUser = null;
    }

    /// <summary>
    /// Сменить пароль
    /// </summary>
    public async Task<(bool Success, string Message)> ChangePasswordAsync(string oldPassword, string newPassword)
    {
        if (CurrentUser == null)
            return (false, "Пользователь не авторизован");

        if (!BCrypt.Net.BCrypt.Verify(oldPassword, CurrentUser.PasswordHash))
            return (false, "Неверный текущий пароль");

        if (newPassword.Length < 6)
            return (false, "Пароль должен содержать не менее 6 символов");

        CurrentUser.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        _userRepository.Update(CurrentUser);
        await _userRepository.SaveChangesAsync();

        return (true, "Пароль успешно изменён");
    }
}
