using System.Windows;
using System.Windows.Controls;
using DispatcherARM.Domain.Entities;
using DispatcherARM.Domain.Enums;
using DispatcherARM.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace DispatcherARM.UI.Views.Pages;

public class UserRow
{
    public int      Id            { get; set; }
    public string   Login         { get; set; } = "";
    public string   FullName      { get; set; } = "";
    public string   RoleDisplay   { get; set; } = "";
    public string?  Email         { get; set; }
    public DateTime RegisteredAt  { get; set; }
    public bool     IsApproved    { get; set; }
    public bool     IsActive      { get; set; }
    public decimal  Balance       { get; set; }
    public UserRole Role          { get; set; }
    public string   StatusDisplay => !IsApproved ? "⏳ Ожидает подтв." : IsActive ? "✅ Активен" : "❌ Заблокирован";
    public string   BalanceDisplay=> $"{Balance:N0} ₽";
}

public partial class AdminUsersPage : UserControl
{
    private List<User> _allUsers = new();

    // Графики работы для новых водителей (чередуем чтобы не совпадали)
    private static readonly string[] _schedules =
        { "5/2 (пн–пт)", "2/2 (ср–чт/вс–пн)", "6/1 (пн–сб)", "4/3 (пн–чт)", "5/2 (вт–сб)" };

    public AdminUsersPage() { InitializeComponent(); }

    private async void Page_Loaded(object sender, RoutedEventArgs e) => await LoadAsync();
    private async void Refresh_Click(object sender, RoutedEventArgs e) => await LoadAsync();

    private async Task LoadAsync()
    {
        try
        {
            using var scope = App.Services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            _allUsers = (await repo.GetAllAsync()).ToList();
            ApplyFilter();
        }
        catch (Exception ex) { MessageBox.Show($"Ошибка: {ex.Message}"); }
    }

    private void ApplyFilter()
    {
        var q = _allUsers.AsEnumerable();
        q = FilterBox.SelectedIndex switch
        {
            1 => q.Where(u => !u.IsApproved),
            2 => q.Where(u =>  u.IsApproved && u.IsActive),
            3 => q.Where(u => !u.IsActive),
            _ => q
        };
        UsersGrid.ItemsSource = q.OrderByDescending(u => u.RegisteredAt)
            .Select(u => new UserRow
            {
                Id           = u.Id,
                Login        = u.Login,
                FullName     = u.FullName,
                Role         = u.Role,
                RoleDisplay  = u.Role switch
                {
                    UserRole.Admin      => "👑 Администратор",
                    UserRole.Dispatcher => "📋 Диспетчер",
                    UserRole.Driver     => "🚗 Водитель",
                    _                   => u.Role.ToString()
                },
                Email        = u.Email,
                RegisteredAt = u.RegisteredAt,
                IsApproved   = u.IsApproved,
                IsActive     = u.IsActive,
                Balance      = u.Balance
            }).ToList();
    }

    private void Filter_Changed(object sender, SelectionChangedEventArgs e) { if (_allUsers.Any()) ApplyFilter(); }

    private void UsersGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var row = UsersGrid.SelectedItem as UserRow;
        bool hasUser = row != null;
        ApproveBtn.IsEnabled      = hasUser && !row!.IsApproved;
        BlockBtn.IsEnabled        = hasUser && row!.IsActive && row!.IsApproved;
        UnblockBtn.IsEnabled      = hasUser && !row!.IsActive;
        ResetPassBtn.IsEnabled    = hasUser;
        PenaltyBonusBtn.IsEnabled = hasUser && row!.Role == UserRole.Driver;
    }

    private async void Approve_Click(object sender, RoutedEventArgs e)
    {
        if (UsersGrid.SelectedItem is not UserRow row) return;

        await UpdateUserAsync(row.Id, u => { u.IsApproved = true; u.IsActive = true; });

        // Если роль — Водитель, создаём запись в таблице Driver
        if (row.Role == UserRole.Driver)
        {
            await CreateDriverIfNeededAsync(row.Id, row.FullName);
        }

        MessageBox.Show(
            $"✅ Аккаунт «{row.Login}» ({row.FullName}) подтверждён!\n\n" +
            (row.Role == UserRole.Driver
                ? "Водитель добавлен во вкладку «Водители».\nПользователь может войти в систему."
                : "Пользователь может войти в систему."),
            "Подтверждение", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async Task CreateDriverIfNeededAsync(int userId, string fullName)
    {
        try
        {
            using var scope = App.Services.CreateScope();
            var driverRepo = scope.ServiceProvider.GetRequiredService<IDriverRepository>();
            var userRepo   = scope.ServiceProvider.GetRequiredService<IUserRepository>();

            // Проверяем, не создан ли уже водитель для этого пользователя
            var existing = (await driverRepo.GetAllAsync()).FirstOrDefault(d => d.UserId == userId);
            if (existing != null) return;

            var user = await userRepo.GetByIdAsync(userId);

            // Считаем количество существующих водителей для подбора расписания
            var driversCount = (await driverRepo.GetAllAsync()).Count();
            var schedule     = _schedules[driversCount % _schedules.Length];

            var driver = new Driver
            {
                FullName      = fullName,
                Phone         = user?.Phone ?? "",
                LicenseNumber = "— (заполните)",
                LicenseCategory = "B",
                Status        = DriverStatus.Available,
                UserId        = userId,
                BaseSalary    = 45000,
                RatePerKm     = 2,
                RatePerDelivery = 150,
                WorkSchedule  = schedule,
                HireDate      = DateTime.Today
            };

            await driverRepo.AddAsync(driver);
            await driverRepo.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Предупреждение: водитель зарегистрирован, но запись не создана: {ex.Message}");
        }
    }

    private async void Block_Click(object sender, RoutedEventArgs e)
    {
        if (UsersGrid.SelectedItem is not UserRow row) return;
        if (MessageBox.Show($"Заблокировать «{row.Login}»?", "Подтверждение",
            MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        await UpdateUserAsync(row.Id, u => u.IsActive = false);
    }

    private async void Unblock_Click(object sender, RoutedEventArgs e)
    {
        if (UsersGrid.SelectedItem is not UserRow row) return;
        await UpdateUserAsync(row.Id, u => u.IsActive = true);
    }

    private async void ResetPass_Click(object sender, RoutedEventArgs e)
    {
        if (UsersGrid.SelectedItem is not UserRow row) return;
        var newPass = $"Pass{row.Id}!{DateTime.Now.Second}";
        await UpdateUserAsync(row.Id, u => u.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPass));
        MessageBox.Show($"🔑 Новый пароль для «{row.Login}»:\n\n{newPass}\n\nПередайте пользователю!", "Пароль сброшен",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async Task UpdateUserAsync(int userId, Action<User> update)
    {
        try
        {
            using var scope = App.Services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            var user = await repo.GetByIdAsync(userId);
            if (user == null) return;
            update(user);
            repo.Update(user);
            await repo.SaveChangesAsync();
            await LoadAsync();
        }
        catch (Exception ex) { MessageBox.Show($"Ошибка: {ex.Message}"); }
    }

    private async void PenaltyBonus_Click(object sender, RoutedEventArgs e)
    {
        if (UsersGrid.SelectedItem is not UserRow row) return;
        if (row.Role != UserRole.Driver)
        {
            MessageBox.Show("Штраф / бонус доступен только для водителей.",
                "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Driver? driver = null;
        try
        {
            using var scope = App.Services.CreateScope();
            var driverRepo = scope.ServiceProvider.GetRequiredService<IDriverRepository>();
            driver = (await driverRepo.GetAllAsync()).FirstOrDefault(d => d.UserId == row.Id);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка загрузки данных водителя: {ex.Message}");
            return;
        }

        if (driver == null)
        {
            MessageBox.Show("Запись водителя не найдена в системе.\nПодтвердите регистрацию пользователя сначала.",
                "Водитель не найден", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var currentUserId = App.Services.GetService(typeof(DispatcherARM.Application.Services.AuthService))
            is DispatcherARM.Application.Services.AuthService auth ? auth.CurrentUser?.Id ?? 0 : 0;

        var dlg = new DispatcherARM.UI.Views.Dialogs.PenaltyBonusDialog(driver, currentUserId)
        {
            Owner = Window.GetWindow(this)
        };
        if (dlg.ShowDialog() == true)
            await LoadAsync();
    }
}
