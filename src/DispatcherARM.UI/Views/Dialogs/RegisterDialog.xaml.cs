using System.Windows;
using DispatcherARM.Domain.Entities;
using DispatcherARM.Domain.Enums;
using DispatcherARM.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace DispatcherARM.UI.Views.Dialogs;

public partial class RegisterDialog : Window
{
    public RegisterDialog() { InitializeComponent(); }

    private async void Register_Click(object sender, RoutedEventArgs e)
    {
        // Валидация
        var name   = FullNameBox.Text.Trim();
        var login  = LoginBox.Text.Trim();
        var pass   = PasswordBox.Password;
        var pass2  = PasswordBox2.Password;

        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(login) || string.IsNullOrEmpty(pass))
        { ShowError("Заполните обязательные поля."); return; }
        if (login.Contains(' ') || login.Any(c => c > 127))
        { ShowError("Логин должен содержать только латиницу и цифры без пробелов."); return; }
        if (pass.Length < 6)
        { ShowError("Пароль должен быть не менее 6 символов."); return; }
        if (pass != pass2)
        { ShowError("Пароли не совпадают."); return; }

        RegisterBtn.IsEnabled = false;

        try
        {
            using var scope = App.Services.CreateScope();
            var userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();

            // Проверяем уникальность логина
            if (await userRepo.ExistsAsync(login))
            { ShowError($"Логин «{login}» уже занят. Выберите другой."); return; }

            var role = RoleBox.SelectedIndex == 1 ? UserRole.Driver : UserRole.Dispatcher;
            var user = new User
            {
                Login        = login,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(pass),
                FullName     = name,
                Role         = role,
                Email        = EmailBox.Text.Trim().Length > 0 ? EmailBox.Text.Trim() : null,
                Phone        = PhoneBox.Text.Trim().Length > 0 ? PhoneBox.Text.Trim() : null,
                IsActive     = false,   // неактивен до одобрения
                IsApproved   = false,   // ждёт подтверждения
                RegisteredAt = DateTime.Now
            };

            await userRepo.AddAsync(user);
            await userRepo.SaveChangesAsync();

            MessageBox.Show(
                "✅ Заявка на регистрацию отправлена!\n\nАдминистратор рассмотрит её и активирует ваш аккаунт.\nПосле активации вы сможете войти в систему.",
                "Заявка принята", MessageBoxButton.OK, MessageBoxImage.Information);

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            ShowError($"Ошибка: {ex.Message}");
        }
        finally
        {
            RegisterBtn.IsEnabled = true;
        }
    }

    private void ShowError(string msg)
    {
        ErrorText.Text = msg;
        ErrorText.Visibility = Visibility.Visible;
    }
}
