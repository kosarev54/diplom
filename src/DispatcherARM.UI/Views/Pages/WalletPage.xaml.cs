using System.Windows;
using System.Windows.Controls;
using DispatcherARM.Domain.Entities;
using DispatcherARM.Domain.Enums;
using DispatcherARM.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace DispatcherARM.UI.Views.Pages;

/// <summary>Строка транзакции для отображения</summary>
public class TransactionRow
{
    public DateTime CreatedAt    { get; set; }
    public string   TypeDisplay  { get; set; } = "";
    public string   Description  { get; set; } = "";
    public string   AmountDisplay{ get; set; } = "";
    public string   StatusDisplay{ get; set; } = "";
    public bool     IsEarning    { get; set; }
    public bool     IsWithdrawal { get; set; }
}

public partial class WalletPage : UserControl
{
    private readonly int _userId;

    public WalletPage(int userId)
    {
        InitializeComponent();
        _userId = userId;
    }

    private async void WalletPage_Loaded(object sender, RoutedEventArgs e) => await LoadAsync();

    private async Task LoadAsync()
    {
        try
        {
            using var scope = App.Services.CreateScope();
            var userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            var txRepo   = scope.ServiceProvider.GetRequiredService<ITransactionRepository>();

            var user = await userRepo.GetByIdAsync(_userId);
            if (user == null) return;

            // Баланс считается из транзакций — единый источник правды
            var driverRepo = scope.ServiceProvider.GetRequiredService<IDriverRepository>();
            var driver = (await driverRepo.GetAllAsync()).FirstOrDefault(d => d.UserId == _userId);

            decimal balance = 0, pending = 0;
            if (driver != null)
            {
                balance = await txRepo.GetBalanceAsync(driver.Id);
                pending = await txRepo.GetPendingAsync(driver.Id);

                // Синхронизируем денормализованное поле, чтобы прочие экраны не разошлись
                if (user.Balance != balance)
                {
                    user.Balance = balance;
                    userRepo.Update(user);
                    await userRepo.SaveChangesAsync();
                }
            }

            BalanceText.Text  = $"{balance:N2} ₽";
            PendingText.Text  = $"{pending:N2} ₽";
            CardText.Text     = user.CardNumber != null
                ? $"Карта: •••• {user.CardNumber[^4..]}"
                : "Карта не привязана";

            var txs = await txRepo.GetByDriverUserIdAsync(_userId);

            decimal totalEarned   = txs.Where(t => t.Type == TransactionType.Earning).AsEnumerable().Sum(t => t.Amount);
            decimal totalWithdrawn= txs.Where(t => t.Type == TransactionType.Withdrawal).AsEnumerable().Sum(t => t.Amount);
            var now = DateTime.Now;
            decimal monthEarned   = txs.Where(t => t.Type == TransactionType.Earning
                                                 && t.CreatedAt.Month == now.Month
                                                 && t.CreatedAt.Year == now.Year)
                                        .Sum(t => t.Amount);

            TotalEarnedText.Text   = $"{totalEarned:N0} ₽";
            TotalWithdrawnText.Text= $"{totalWithdrawn:N0} ₽";
            MonthEarnedText.Text   = $"{monthEarned:N0} ₽";

            TransactionsGrid.ItemsSource = txs
                .OrderByDescending(t => t.CreatedAt)
                .Select(t => new TransactionRow
                {
                    CreatedAt    = t.CreatedAt,
                    TypeDisplay  = t.Type switch
                    {
                        TransactionType.Earning    => "➕ Начисление",
                        TransactionType.Withdrawal => "➖ Вывод",
                        TransactionType.Bonus      => "🎁 Бонус",
                        TransactionType.Penalty    => "⚠ Штраф",
                        _                          => t.Type.ToString()
                    },
                    Description   = t.Description ?? "—",
                    // ── P1-14 FIX: Penalty и Withdrawal — знак «−» ────────────────────
                    AmountDisplay = t.Type is TransactionType.Withdrawal or TransactionType.Penalty
                        ? $"−{t.Amount:N0} ₽"
                        : $"+{t.Amount:N0} ₽",
                    StatusDisplay = t.IsConfirmed ? "✅ Выполнено" : "⏳ Ожидание",
                    IsEarning    = t.Type is TransactionType.Earning or TransactionType.Bonus,
                    IsWithdrawal = t.Type is TransactionType.Withdrawal or TransactionType.Penalty
                })
                .ToList();
        }
        catch (Exception ex) { MessageBox.Show($"Ошибка загрузки кошелька: {ex.Message}"); }
    }

    private async void Withdraw_Click(object sender, RoutedEventArgs e)
    {
        using var scope = App.Services.CreateScope();
        var userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var user = await userRepo.GetByIdAsync(_userId);
        if (user == null) return;

        if (user.Balance < 100)
        {
            MessageBox.Show("Минимальная сумма вывода — 100 ₽.", "Недостаточно средств");
            return;
        }

        var dlg = new DispatcherARM.UI.Views.Dialogs.WithdrawDialog(user)
        {
            Owner = Window.GetWindow(this)
        };
        if (dlg.ShowDialog() == true) await LoadAsync();
    }
}
