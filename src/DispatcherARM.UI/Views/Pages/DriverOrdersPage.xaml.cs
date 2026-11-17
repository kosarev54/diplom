using System.Windows;
using System.Windows.Controls;
using DispatcherARM.Domain.Entities;
using DispatcherARM.Domain.Enums;
using DispatcherARM.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace DispatcherARM.UI.Views.Pages;

/// <summary>ViewModel строки заказа для таблицы водителя</summary>
public class DriverOrderRow
{
    public int         Id                { get; set; }
    public string      OrderNumber       { get; set; } = "";
    public string      ClientName        { get; set; } = "";
    public string      Address           { get; set; } = "";
    public double      Weight            { get; set; }
    public decimal     DeliveryPrice     { get; set; }
    public DateTime?   DeliveryDate      { get; set; }
    public DateTime?   CompletedAt       { get; set; }
    public OrderStatus Status            { get; set; }
    public OrderPriority Priority        { get; set; }

    // Выполнен водителем, ждёт подтверждения администратора
    public bool IsWaitingConfirm => Status == OrderStatus.InTransit && CompletedAt.HasValue;

    public string StatusDisplay => Status switch
    {
        OrderStatus.Assigned  => "⏳ Назначен — ожидает погрузки",
        OrderStatus.Loading   => "📦 Погружен — готов к отправке!",
        OrderStatus.InTransit when !CompletedAt.HasValue => "🚗 В работе — едет к клиенту",
        OrderStatus.InTransit => "🕐 Ожидает подтверждения адм.",
        OrderStatus.Delivered => "✅ Выполнен и оплачен",
        _                     => Status.ToString()
    };

    public string PriorityDisplay => Priority switch
    {
        OrderPriority.Urgent => "🔴 Срочный",
        OrderPriority.High   => "🟠 Высокий",
        OrderPriority.Normal => "🟡 Обычный",
        OrderPriority.Low    => "🟢 Низкий",
        _                    => Priority.ToString()
    };
}

public partial class DriverOrdersPage : UserControl
{
    private int _currentDriverId = 0;
    private readonly int _currentUserId;

    public DriverOrdersPage(int userId)
    {
        InitializeComponent();
        _currentUserId = userId;
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e) => await LoadAsync();
    private async void Refresh_Click(object sender, RoutedEventArgs e) => await LoadAsync();

    private async Task LoadAsync()
    {
        try
        {
            using var scope = App.Services.CreateScope();
            var driverRepo = scope.ServiceProvider.GetRequiredService<IDriverRepository>();
            var orderRepo  = scope.ServiceProvider.GetRequiredService<IOrderRepository>();
            var txRepo     = scope.ServiceProvider.GetRequiredService<ITransactionRepository>();

            // ── Найти Driver по UserId ──
            var drivers  = await driverRepo.GetAllAsync();
            var myDriver = drivers.FirstOrDefault(d => d.UserId == _currentUserId);

            if (myDriver == null)
            {
                DriverGreeting.Text    = "Профиль водителя не найден";
                DriverStatusInfo.Text  = "Обратитесь к администратору";
                BalanceText.Text       = "— ₽";
                return;
            }

            _currentDriverId = myDriver.Id;
            DriverGreeting.Text   = myDriver.FullName;
            DriverStatusInfo.Text = $"📋 {myDriver.WorkSchedule ?? "—"}  |  📞 {myDriver.Phone}";

            // ── Баланс ──
            var balance = await txRepo.GetBalanceAsync(myDriver.Id);
            BalanceText.Text = $"{balance:N0} ₽";

            // ── Все заказы этого водителя ──
            var allOrders = (await orderRepo.GetAllAsync()).ToList();

            // Вкладка 1: Доступные — назначены (Assigned) или погружены (Loading)
            var available = allOrders
                .Where(o => o.AssignedDriverId == _currentDriverId
                         && o.Status is OrderStatus.Assigned or OrderStatus.Loading)
                .OrderBy(o => o.Priority)
                .ThenBy(o => o.DeliveryDate)
                .Select(ToRow).ToList();

            AvailableGrid.ItemsSource = available;

            // Счётчик-баджик
            if (available.Count > 0)
            {
                AvailableBadge.Visibility = Visibility.Visible;
                AvailableCount.Text       = available.Count.ToString();
            }
            else
            {
                AvailableBadge.Visibility = Visibility.Collapsed;
            }

            // Вкладка 2: Мои заказы — InTransit (и ждут подтверждения, и активные)
            var myOrders = allOrders
                .Where(o => o.AssignedDriverId == _currentDriverId
                         && o.Status == OrderStatus.InTransit)
                .OrderBy(o => o.DeliveryDate)
                .Select(ToRow).ToList();

            MyOrdersGrid.ItemsSource = myOrders;

            if (myOrders.Count > 0)
            {
                MyOrdersBadge.Visibility = Visibility.Visible;
                MyOrdersCount.Text       = myOrders.Count.ToString();
            }
            else
            {
                MyOrdersBadge.Visibility = Visibility.Collapsed;
            }

            // Вкладка 3: История — выполненные и подтверждённые
            HistoryGrid.ItemsSource = allOrders
                .Where(o => o.AssignedDriverId == _currentDriverId
                         && o.Status == OrderStatus.Delivered)
                .OrderByDescending(o => o.CompletedAt)
                .Select(ToRow).ToList();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка загрузки: {ex.Message}");
        }
    }

    private static DriverOrderRow ToRow(Order o) => new()
    {
        Id            = o.Id,
        OrderNumber   = o.OrderNumber,
        ClientName    = o.ClientName,
        Address       = o.Address,
        Weight        = o.Weight,
        DeliveryPrice = o.DeliveryPrice,
        DeliveryDate  = o.DeliveryDate,
        CompletedAt   = o.CompletedAt,
        Status        = o.Status,
        Priority      = o.Priority
    };

    // ─── Выбор в «Доступные» ────────────────────────────────────────────────────
    private void Available_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var row = AvailableGrid.SelectedItem as DriverOrderRow;
        TakeOrderBtn.IsEnabled = row != null;
        AvailableHint.Text = row != null
            ? $"Заказ: {row.OrderNumber} · {row.ClientName} · {row.Address}"
            : "Выберите заказ и нажмите «Взять заказ»";
    }

    // ─── Взять заказ (диспетчер назначил → водитель принимает) ─────────────────
    private async void TakeOrder_Click(object sender, RoutedEventArgs e)
    {
        if (AvailableGrid.SelectedItem is not DriverOrderRow row) return;

        var confirm = MessageBox.Show(
            $"Взять заказ {row.OrderNumber} в работу?\n\n" +
            $"Клиент: {row.ClientName}\nАдрес: {row.Address}\n" +
            $"Дата доставки: {row.DeliveryDate:dd.MM.yyyy}",
            "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            using var scope = App.Services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IOrderRepository>();
            var o = await repo.GetByIdAsync(row.Id);
            if (o == null) return;

            // Assigned → InTransit (водитель принял и едет)
            o.Status = OrderStatus.InTransit;
            repo.Update(o);
            await repo.SaveChangesAsync();

            MessageBox.Show(
                $"✅ Заказ {row.OrderNumber} принят в работу!\n" +
                "Он переместился во вкладку «Мои заказы».\n\n" +
                "После доставки нажмите «Отметить выполненным».",
                "Заказ принят", MessageBoxButton.OK, MessageBoxImage.Information);

            await LoadAsync();
        }
        catch (Exception ex) { MessageBox.Show($"Ошибка: {ex.Message}"); }
    }

    // ─── Выбор в «Мои заказы» ───────────────────────────────────────────────────
    private void MyOrders_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var row = MyOrdersGrid.SelectedItem as DriverOrderRow;
        // Можно отметить только те, которые ещё не ждут подтверждения
        CompleteBtn.IsEnabled = row != null && !row.IsWaitingConfirm;
        MyOrdersHint.Text = row == null
            ? "Выберите активный заказ для отметки выполнения"
            : row.IsWaitingConfirm
                ? $"⏳ Заказ {row.OrderNumber} ожидает подтверждения администратора"
                : $"Заказ {row.OrderNumber} · {row.ClientName}";
    }

    // ─── Отметить выполненным (ждёт подтверждения администратора) ───────────────
    private async void MarkComplete_Click(object sender, RoutedEventArgs e)
    {
        if (MyOrdersGrid.SelectedItem is not DriverOrderRow row) return;

        var confirm = MessageBox.Show(
            $"Отметить заказ {row.OrderNumber} как выполненный?\n\n" +
            $"Клиент: {row.ClientName}\nАдрес: {row.Address}\n\n" +
            "⚠️ После отметки заказ уйдёт на подтверждение администратору.\n" +
            "Оплата будет начислена после подтверждения.",
            "Подтверждение выполнения", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            using var scope = App.Services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IOrderRepository>();
            var o = await repo.GetByIdAsync(row.Id);
            if (o == null) return;

            // Остаётся InTransit, но CompletedAt выставлен → администратор видит в «Подтвердить»
            o.Status      = OrderStatus.InTransit;
            o.CompletedAt = DateTime.Now;
            repo.Update(o);
            await repo.SaveChangesAsync();

            MessageBox.Show(
                "📨 Выполнение отправлено администратору на подтверждение!\n\n" +
                "Как только администратор подтвердит — оплата поступит на ваш баланс.\n" +
                "Заказ переместится в «История выплат».",
                "Ожидание подтверждения", MessageBoxButton.OK, MessageBoxImage.Information);

            await LoadAsync();
        }
        catch (Exception ex) { MessageBox.Show($"Ошибка: {ex.Message}"); }
    }
}
