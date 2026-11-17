using System.Globalization;
using System.Windows;
using DispatcherARM.Domain.Entities;
using DispatcherARM.Domain.Enums;
using DispatcherARM.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace DispatcherARM.UI.Views.Dialogs;

public partial class PenaltyBonusDialog : Window
{
    private readonly Driver _driver;
    private readonly int _userId;

    public PenaltyBonusDialog(Driver driver, int userId)
    {
        InitializeComponent();
        _driver = driver;
        _userId = userId;
        DriverInfoText.Text = $"Водитель: {driver.FullName}  |  {driver.Phone}";
        DialogTitle.Text = $"Штраф / Бонус — {driver.FullName}";
    }

    private async void Confirm_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Visibility = Visibility.Collapsed;

        if (!decimal.TryParse(AmountBox.Text.Trim(), NumberStyles.Any,
            CultureInfo.InvariantCulture, out var amount) || amount <= 0)
        {
            ErrorText.Text = "Введите корректную сумму (больше 0)";
            ErrorText.Visibility = Visibility.Visible;
            return;
        }
        if (string.IsNullOrWhiteSpace(CommentBox.Text))
        {
            ErrorText.Text = "Комментарий обязателен — укажите причину";
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        var txType = PenaltyRadio.IsChecked == true
            ? TransactionType.Penalty
            : TransactionType.Bonus;

        try
        {
            ConfirmBtn.IsEnabled = false;
            using var scope = App.Services.CreateScope();
            var txRepo   = scope.ServiceProvider.GetRequiredService<ITransactionRepository>();
            var userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();

            var tx = new DriverTransaction
            {
                DriverId    = _driver.Id,
                Amount      = amount,
                Type        = txType,
                IsConfirmed = true,
                Description = $"{(txType == TransactionType.Penalty ? "Штраф" : "Бонус")}: {CommentBox.Text.Trim()}",
                CreatedAt   = DateTime.Now
            };
            await txRepo.AddAsync(tx);
            await txRepo.SaveChangesAsync();

            // Обновляем User.Balance из журнала транзакций (единый источник правды)
            if (_driver.UserId.HasValue)
            {
                var user = await userRepo.GetByIdAsync(_driver.UserId.Value);
                if (user != null)
                {
                    user.Balance = await txRepo.GetBalanceAsync(_driver.Id);
                    userRepo.Update(user);
                    await userRepo.SaveChangesAsync();
                }
            }

            var verb = txType == TransactionType.Penalty ? "Штраф" : "Бонус";
            MessageBox.Show($"{verb} {amount:N0} ₽ применён.\nКомментарий: {CommentBox.Text.Trim()}",
                "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
        }
        catch (Exception ex)
        {
            ErrorText.Text = $"Ошибка: {ex.Message}";
            ErrorText.Visibility = Visibility.Visible;
            ConfirmBtn.IsEnabled = true;
        }
    }
}
