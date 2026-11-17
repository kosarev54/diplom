using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using DispatcherARM.Application.Services;
using DispatcherARM.Domain.Interfaces;
using DispatcherARM.UI.Views.Dialogs;
using Microsoft.Extensions.DependencyInjection;

namespace DispatcherARM.UI.Views;

/// <summary>
/// Окно авторизации: 4 попытки, блокировка 30 сек, проверка статуса аккаунта
/// </summary>
public partial class LoginView : Window
{
    private int  _attemptsLeft = 4;
    private bool _isLocked     = false;

    public LoginView()
    {
        InitializeComponent();
        LoginBox.Focus();
    }

    // ─── Проверяем статус при вводе логина ────────────────────────────────────
    private async void LoginBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        var login = LoginBox.Text.Trim();
        if (login.Length < 3) { StatusPanel.Visibility = Visibility.Collapsed; return; }

        try
        {
            using var scope = App.Services.CreateScope();
            var userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            var user = await userRepo.GetByLoginAsync(login);

            if (user == null)
            {
                StatusBorder.Visibility = Visibility.Collapsed;
                return;
            }

            StatusBorder.Visibility = Visibility.Visible;

            if (!user.IsApproved)
            {
                StatusIcon.Text        = "⏳";
                StatusText.Text        = "Аккаунт ожидает подтверждения администратором";
                StatusBorder.Background= new SolidColorBrush(Color.FromRgb(255, 248, 225));
                StatusText.Foreground  = new SolidColorBrush(Color.FromRgb(230, 81, 0));
            }
            else if (!user.IsActive)
            {
                StatusIcon.Text        = "🚫";
                StatusText.Text        = "Аккаунт заблокирован. Обратитесь к администратору";
                StatusBorder.Background= new SolidColorBrush(Color.FromRgb(255, 235, 238));
                StatusText.Foreground  = new SolidColorBrush(Color.FromRgb(198, 40, 40));
            }
            else
            {
                StatusIcon.Text        = "✅";
                StatusText.Text        = "Аккаунт активен. Введите пароль для входа";
                StatusBorder.Background= new SolidColorBrush(Color.FromRgb(232, 245, 233));
                StatusText.Foreground  = new SolidColorBrush(Color.FromRgb(46, 125, 50));
            }
        }
        catch
        {
            StatusBorder.Visibility = Visibility.Collapsed;
        }
    }

    // ─── Вход ─────────────────────────────────────────────────────────────────
    private async void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isLocked) return;
        if (string.IsNullOrWhiteSpace(LoginBox.Text) || PasswordBox.Password.Length == 0)
        {
            ShowError("Введите логин и пароль.");
            return;
        }

        LoginButton.IsEnabled  = false;
        ProgressBar.Visibility = Visibility.Visible;
        ErrorPanel.Visibility  = Visibility.Collapsed;

        try
        {
            using var scope = App.Services.CreateScope();

            // Сначала проверяем статус аккаунта
            var userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            var userCheck = await userRepo.GetByLoginAsync(LoginBox.Text.Trim());

            if (userCheck != null && !userCheck.IsApproved)
            {
                ShowInfo("⏳ Ваш аккаунт ещё не подтверждён администратором.\nОжидайте уведомления или обратитесь к администратору.");
                return;
            }
            if (userCheck != null && !userCheck.IsActive)
            {
                ShowInfo("🚫 Ваш аккаунт заблокирован.\nОбратитесь к администратору системы.");
                return;
            }

            var authService = scope.ServiceProvider.GetRequiredService<AuthService>();
            var (success, message) = await authService.LoginAsync(LoginBox.Text.Trim(), PasswordBox.Password);

            if (success)
            {
                var mainWindow = new MainWindow(authService.CurrentUser!);
                mainWindow.Show();
                Close();
            }
            else
            {
                _attemptsLeft--;
                if (_attemptsLeft <= 0)
                    await LockAccountAsync();
                else
                {
                    ShowError(message ?? "Неверный пароль.");
                    AttemptsText.Text = $"Осталось попыток: {_attemptsLeft}";
                }
            }
        }
        catch (Exception ex)
        {
            ShowError($"Ошибка подключения: {ex.Message}");
        }
        finally
        {
            if (!_isLocked) LoginButton.IsEnabled = true;
            ProgressBar.Visibility = Visibility.Collapsed;
        }
    }

    private async Task LockAccountAsync()
    {
        _isLocked = true;
        LoginButton.IsEnabled = false;
        for (int i = 30; i > 0; i--)
        {
            ShowError("Слишком много неверных попыток.");
            AttemptsText.Text = $"Вход заблокирован на {i} секунд...";
            await Task.Delay(1000);
        }
        _isLocked      = false;
        _attemptsLeft  = 4;
        LoginButton.IsEnabled    = true;
        ErrorPanel.Visibility    = Visibility.Collapsed;
        PasswordBox.Password     = "";
        LoginBox.Focus();
    }

    private void ShowError(string msg)
    {
        ErrorText.Text      = msg;
        ErrorText.Foreground= new SolidColorBrush(Colors.Red);
        ErrorPanel.Visibility = Visibility.Visible;
    }

    private void ShowInfo(string msg)
    {
        ErrorText.Text      = msg;
        ErrorText.Foreground= new SolidColorBrush(Color.FromRgb(230, 81, 0));
        AttemptsText.Text   = "";
        ErrorPanel.Visibility = Visibility.Visible;
    }

    private void Input_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !_isLocked)
            LoginButton_Click(sender, new RoutedEventArgs());
    }

    private void Register_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new RegisterDialog { Owner = this };
        if (dlg.ShowDialog() == true)
        {
            // После регистрации показываем подсказку
            ShowInfo("✅ Заявка отправлена! Ожидайте подтверждения администратора.\nПосле одобрения вы сможете войти в систему.");
        }
    }
}
