using System.Windows;
using DispatcherARM.Application.Services;
using DispatcherARM.Domain.Interfaces;
using DispatcherARM.UI.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DispatcherARM.UI.Views.Dialogs;

/// <summary>
/// Диалог смены пароля текущего пользователя.
/// Принимает userId и login; всё сохранение выполняет внутри.
/// </summary>
public partial class ChangePasswordDialog : Window
{
    private readonly int _userId;
    private readonly string _login;

    public ChangePasswordDialog(int userId, string login)
    {
        InitializeComponent();
        _userId = userId;
        _login  = login;
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        var current = CurrentPasswordBox.Password;
        var newPwd  = NewPasswordBox.Password;
        var confirm = ConfirmPasswordBox.Password;

        if (string.IsNullOrWhiteSpace(current) || string.IsNullOrWhiteSpace(newPwd))
        {
            ShowError("Все поля обязательны для заполнения.");
            return;
        }

        if (newPwd != confirm)
        {
            ShowError("Новый пароль и подтверждение не совпадают.");
            return;
        }

        if (newPwd.Length < 6)
        {
            ShowError("Новый пароль должен содержать не менее 6 символов.");
            return;
        }

        try
        {
            using var scope = App.Services.CreateScope();
            var authService = scope.ServiceProvider.GetRequiredService<AuthService>();

            // Проверяем текущий пароль через AuthService
            var (ok, msg) = await authService.LoginAsync(_login, current);
            if (!ok)
            {
                ShowError("Неверный текущий пароль.");
                return;
            }

            // Меняем пароль
            var userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            var user = await userRepo.GetByIdAsync(_userId);
            if (user == null) { ShowError("Пользователь не найден."); return; }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPwd);
            userRepo.Update(user);
            await userRepo.SaveChangesAsync();

            AppLogger.Info($"Пользователь '{_login}' сменил пароль.");
            MessageBox.Show("Пароль успешно изменён!", "Готово",
                MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
        }
        catch (Exception ex)
        {
            AppLogger.Error("Ошибка смены пароля", ex);
            ShowError($"Ошибка: {ex.Message}");
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

    private void ShowError(string message)
    {
        ErrorText.Text       = message;
        ErrorText.Visibility = Visibility.Visible;
    }
}
