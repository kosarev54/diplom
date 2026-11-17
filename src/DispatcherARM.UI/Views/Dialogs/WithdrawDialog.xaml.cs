using System.Windows;
using DispatcherARM.Domain.Entities;
using DispatcherARM.Domain.Enums;
using DispatcherARM.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace DispatcherARM.UI.Views.Dialogs;

public partial class WithdrawDialog : Window
{
    private readonly User _user;

    public WithdrawDialog(User user)
    {
        InitializeComponent();
        _user = user;
        AvailableText.Text = $"Доступно: {user.Balance:N2} ₽";
        if (!string.IsNullOrEmpty(user.CardNumber))
            CardBox.Text = user.CardNumber;
    }

    private async void Withdraw_Click(object sender, RoutedEventArgs e)
    {
        if (!decimal.TryParse(AmountBox.Text, out var amount) || amount <= 0)
        { ShowError("Введите корректную сумму."); return; }
        if (amount > _user.Balance)
        { ShowError($"Недостаточно средств. Доступно: {_user.Balance:N2} ₽."); return; }
        if (CardBox.Text.Length != 16 || !CardBox.Text.All(char.IsDigit))
        { ShowError("Введите корректный номер карты (16 цифр)."); return; }
        if (string.IsNullOrWhiteSpace(CardOwnerBox.Text))
        { ShowError("Введите имя владельца карты."); return; }

        WithdrawBtn.IsEnabled = false;
        try
        {
            using var scope = App.Services.CreateScope();
            var userRepo  = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            var txRepo    = scope.ServiceProvider.GetRequiredService<ITransactionRepository>();
            var driverRepo= scope.ServiceProvider.GetRequiredService<IDriverRepository>();

            var user = await userRepo.GetByIdAsync(_user.Id);
            if (user == null) { ShowError("Ошибка: пользователь не найден."); return; }

            // Найдём водителя по userId
            var allDrivers = await driverRepo.GetAllAsync();
            var driver = allDrivers.FirstOrDefault(d => d.UserId == _user.Id);

            // Баланс — из транзакций (единый источник правды), не из денормализованного поля
            var realBalance = driver != null
                ? await txRepo.GetBalanceAsync(driver.Id)
                : user.Balance;
            if (amount > realBalance)
            {
                ShowError($"Недостаточно средств. Доступно: {realBalance:N2} ₽.");
                return;
            }

            if (driver != null)
            {
                var tx = new DriverTransaction
                {
                    DriverId         = driver.Id,
                    Type             = TransactionType.Withdrawal,
                    Amount           = amount,
                    Description      = $"Вывод на карту •••• {CardBox.Text[^4..]} ({CardOwnerBox.Text})",
                    IsConfirmed      = true,
                    WithdrawalDetails= CardBox.Text
                };
                await txRepo.AddAsync(tx);
                await txRepo.SaveChangesAsync();
            }
            else
            {
                // Водитель не привязан к пользователю — вычитать неоткуда
                ShowError("Учётная запись не связана с водителем. Обратитесь к администратору.");
                return;
            }

            // Денормализованное поле синхронизируем с журналом транзакций
            user.Balance = await txRepo.GetBalanceAsync(driver.Id);
            if (SaveCardCheck.IsChecked == true)
                user.CardNumber = CardBox.Text;
            userRepo.Update(user);
            await userRepo.SaveChangesAsync();

            MessageBox.Show($"✅ Заявка на вывод {amount:N2} ₽ принята!\nСредства поступят на карту в течение 1-3 рабочих дней.",
                "Вывод средств", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
            Close();
        }
        catch (Exception ex) { ShowError($"Ошибка: {ex.Message}"); }
        finally { WithdrawBtn.IsEnabled = true; }
    }

    private void ShowError(string msg) { ErrorText.Text = msg; ErrorText.Visibility = Visibility.Visible; }
}
