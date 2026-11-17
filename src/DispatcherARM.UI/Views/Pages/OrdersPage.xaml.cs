using System.Windows;
using System.Windows.Controls;
using DispatcherARM.Domain.Entities;
using DispatcherARM.Domain.Enums;
using DispatcherARM.Domain.Interfaces;
using DispatcherARM.UI.Services;
using DispatcherARM.UI.Views.Dialogs;
using Microsoft.Extensions.DependencyInjection;

namespace DispatcherARM.UI.Views.Pages;

public partial class OrdersPage : UserControl
{
    private List<Order> _allOrders = new();

    public OrdersPage() { InitializeComponent(); }

    private async void OrdersPage_Loaded(object sender, RoutedEventArgs e) => await LoadOrdersAsync();

    private async Task LoadOrdersAsync()
    {
        try
        {
            using var scope = App.Services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IOrderRepository>();
            _allOrders = (await repo.GetAllAsync()).ToList();
            ApplyFilters();
            CountBadge.Text = $"Всего записей: {_allOrders.Count}";
        }
        catch (Exception ex) { MessageBox.Show($"Ошибка загрузки: {ex.Message}"); }
    }

    private void ApplyFilters()
    {
        var q = _allOrders.AsEnumerable();

        // Фильтр по статусу
        if (StatusFilter.SelectedIndex > 0)
        {
            var statusMap = new[]
            {
                OrderStatus.New, OrderStatus.Assigned, OrderStatus.Loading,
                OrderStatus.InTransit, OrderStatus.Delivered, OrderStatus.Cancelled
            };
            var idx = StatusFilter.SelectedIndex - 1;
            if (idx < statusMap.Length)
                q = q.Where(o => o.Status == statusMap[idx]);
        }

        // Поиск
        var s = SearchBox.Text?.Trim().ToLower();
        if (!string.IsNullOrEmpty(s))
            q = q.Where(o => o.ClientName.ToLower().Contains(s)
                           || o.Address.ToLower().Contains(s)
                           || o.OrderNumber.ToLower().Contains(s)
                           || (o.AssignedDriver?.FullName?.ToLower().Contains(s) ?? false));

        // Фильтр по дате
        if (DateFilter.SelectedDate.HasValue)
            q = q.Where(o => o.DeliveryDate?.Date == DateFilter.SelectedDate.Value.Date);

        var result = q.OrderByDescending(o => o.CreatedAt).ToList();
        OrdersGrid.ItemsSource = result;
        GridCountText.Text = $"Показано: {result.Count} из {_allOrders.Count}";
    }

    private void StatusFilter_Changed(object sender, SelectionChangedEventArgs e)
    { if (_allOrders.Any()) ApplyFilters(); }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    { if (_allOrders.Any()) ApplyFilters(); }

    private void DateFilter_Changed(object sender, SelectionChangedEventArgs e)
    { if (_allOrders.Any()) ApplyFilters(); }

    private void ClearFilters_Click(object sender, RoutedEventArgs e)
    {
        StatusFilter.SelectedIndex = 0;
        SearchBox.Text = "";
        DateFilter.SelectedDate = null;
        ApplyFilters();
    }

    private async void RefreshOrders_Click(object sender, RoutedEventArgs e) => await LoadOrdersAsync();

    private void OrdersGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var order = OrdersGrid.SelectedItem as Order;
        bool has  = order != null;

        AssignBtn.IsEnabled  = has && order!.Status is OrderStatus.New or OrderStatus.Assigned;
        LoadingBtn.IsEnabled = has && order!.Status == OrderStatus.Assigned;
        ConfirmBtn.IsEnabled = has && order!.Status == OrderStatus.InTransit && order!.CompletedAt.HasValue;
        CancelBtn.IsEnabled  = has && order!.Status is OrderStatus.New or OrderStatus.Assigned;
        EditBtn.IsEnabled    = has && order!.Status == OrderStatus.New;
        WaybillBtn.IsEnabled = has && order!.AssignedDriverId.HasValue;
        TtnBtn.IsEnabled     = has && order!.AssignedDriverId.HasValue;

        if (has)
        {
            SelectedInfoText.Text = $"Выбрано: {order!.OrderNumber} — {order.ClientName} | " +
                                    $"Статус: {order.StatusDisplay} | Водитель: {order.AssignedDriver?.FullName ?? "не назначен"}";
        }
        else
        {
            SelectedInfoText.Text = "Выберите запись для работы с ней";
        }
    }

    // ─── Назначить водителя ─────────────────────────────────────────────────────
    private async void AssignOrder_Click(object sender, RoutedEventArgs e)
    {
        if (OrdersGrid.SelectedItem is not Order order) return;
        var dlg = new AssignOrderDialog(order) { Owner = Window.GetWindow(this) };
        if (dlg.ShowDialog() != true) return;

        try
        {
            using var scope = App.Services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IOrderRepository>();
            var o = await repo.GetByIdAsync(order.Id);
            if (o == null) return;
            o.AssignedDriverId = dlg.SelectedDriverId;
            o.DeliveryDate     = dlg.SelectedDeliveryDate;
            o.Status           = OrderStatus.Assigned;
            repo.Update(o);
            await repo.SaveChangesAsync();
            MessageBox.Show($"Заказ {o.OrderNumber} назначен водителю.\nДата доставки: {o.DeliveryDate:dd.MM.yyyy}",
                "Назначено", MessageBoxButton.OK, MessageBoxImage.Information);
            await LoadOrdersAsync();
        }
        catch (Exception ex) { MessageBox.Show($"Ошибка: {ex.Message}"); }
    }

    // ─── Подтвердить выполнение ─────────────────────────────────────────────────
    private async void ConfirmOrder_Click(object sender, RoutedEventArgs e)
    {
        if (OrdersGrid.SelectedItem is not Order order) return;
        if (MessageBox.Show($"Подтвердить выполнение заказа {order.OrderNumber}?\n\nПосле подтверждения водителю будет начислена оплата.",
            "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

        try
        {
            using var scope    = App.Services.CreateScope();
            var orderRepo      = scope.ServiceProvider.GetRequiredService<IOrderRepository>();
            var driverRepo     = scope.ServiceProvider.GetRequiredService<IDriverRepository>();
            var txRepo         = scope.ServiceProvider.GetRequiredService<ITransactionRepository>();
            var userRepo       = scope.ServiceProvider.GetRequiredService<IUserRepository>();

            var o = await orderRepo.GetByIdAsync(order.Id);
            if (o == null) return;
            o.Status      = OrderStatus.Delivered;
            o.CompletedAt = DateTime.Now;
            o.IsPaid      = true;
            orderRepo.Update(o);
            await orderRepo.SaveChangesAsync();

            if (o.AssignedDriverId.HasValue)
            {
                var driver = await driverRepo.GetByIdAsync(o.AssignedDriverId.Value);
                if (driver != null)
                {
                    // Начисление: ставка за доставку + комиссия из настроек (по умолчанию 15%)
                    var commRate = (decimal)AppSettings.Instance.DriverCommissionRate;
                    var amount = driver.RatePerDelivery + (o.DeliveryPrice * commRate);

                    // ── БАГ 1 FIX: IsConfirmed=true + обновить User.Balance ────────
                    var tx = new DriverTransaction
                    {
                        DriverId    = driver.Id,
                        Amount      = amount,
                        Type        = TransactionType.Earning,
                        IsConfirmed = true,   // ← обязательно, иначе GetBalanceAsync вернёт 0
                        Description = $"Выплата за заказ {o.OrderNumber} ({o.ClientName})",
                        OrderId     = o.Id,
                        CreatedAt   = DateTime.Now
                    };
                    await txRepo.AddAsync(tx);
                    await txRepo.SaveChangesAsync();

                    // Обновляем User.Balance (денормализованное поле — берём из журнала транзакций)
                    if (driver.UserId.HasValue)
                    {
                        var driverUser = await userRepo.GetByIdAsync(driver.UserId.Value);
                        if (driverUser != null)
                        {
                            driverUser.Balance = await txRepo.GetBalanceAsync(driver.Id);
                            userRepo.Update(driverUser);
                            await userRepo.SaveChangesAsync();
                        }
                    }

                    // Освобождаем водителя (Available) если у него нет других активных заказов
                    var activeOrders = (await orderRepo.GetAllAsync())
                        .Where(x => x.AssignedDriverId == driver.Id
                               && x.Status is OrderStatus.Assigned or OrderStatus.Loading or OrderStatus.InTransit)
                        .Any();
                    if (!activeOrders)
                    {
                        driver.Status = DriverStatus.Available;
                        driverRepo.Update(driver);
                        await driverRepo.SaveChangesAsync();
                    }

                    MessageBox.Show($"Выполнение подтверждено!\n\nВодителю {driver.FullName} начислено: {amount:N0} ₽",
                        "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            await LoadOrdersAsync();
        }
        catch (Exception ex) { MessageBox.Show($"Ошибка: {ex.Message}"); }
    }

    // ─── Отменить заказ ─────────────────────────────────────────────────────────
    private async void CancelOrder_Click(object sender, RoutedEventArgs e)
    {
        if (OrdersGrid.SelectedItem is not Order order) return;
        if (MessageBox.Show($"Отменить заказ {order.OrderNumber}?", "Подтверждение",
            MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        try
        {
            using var scope = App.Services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IOrderRepository>();
            var o = await repo.GetByIdAsync(order.Id);
            if (o == null) return;
            o.Status = OrderStatus.Cancelled;
            repo.Update(o);
            await repo.SaveChangesAsync();
            await LoadOrdersAsync();
        }
        catch (Exception ex) { MessageBox.Show($"Ошибка: {ex.Message}"); }
    }

    // ─── Изменить / Добавить ────────────────────────────────────────────────────
    private async void EditOrder_Click(object sender, RoutedEventArgs e)
    {
        if (OrdersGrid.SelectedItem is not Order order) return;
        var dlg = new OrderDialog(order) { Owner = Window.GetWindow(this) };
        if (dlg.ShowDialog() == true) await LoadOrdersAsync();
    }

    private async void AddOrder_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OrderDialog() { Owner = Window.GetWindow(this) };
        if (dlg.ShowDialog() == true) await LoadOrdersAsync();
    }

    // ─── Путевой лист ────────────────────────────────────────────────────────────
    private async void Waybill_Click(object sender, RoutedEventArgs e)
    {
        if (OrdersGrid.SelectedItem is not Order order) return;
        try
        {
            using var scope = App.Services.CreateScope();
            var orderRepo  = scope.ServiceProvider.GetRequiredService<IOrderRepository>();
            var driverRepo = scope.ServiceProvider.GetRequiredService<IDriverRepository>();
            var o = await orderRepo.GetByIdAsync(order.Id);
            Driver? driver = null;
            if (o?.AssignedDriverId.HasValue == true)
                driver = await driverRepo.GetByIdAsync(o.AssignedDriverId.Value);
            var dlg = new WaybillDialog(o ?? order, driver) { Owner = Window.GetWindow(this) };
            dlg.ShowDialog();
        }
        catch (Exception ex) { MessageBox.Show($"Ошибка: {ex.Message}"); }
    }

    // ─── ТТН ─────────────────────────────────────────────────────────────────────
    private async void Ttn_Click(object sender, RoutedEventArgs e)
    {
        if (OrdersGrid.SelectedItem is not Order order) return;
        try
        {
            using var scope = App.Services.CreateScope();
            var orderRepo  = scope.ServiceProvider.GetRequiredService<IOrderRepository>();
            var driverRepo = scope.ServiceProvider.GetRequiredService<IDriverRepository>();
            var o = await orderRepo.GetByIdAsync(order.Id);
            Driver? driver = null;
            if (o?.AssignedDriverId.HasValue == true)
                driver = await driverRepo.GetByIdAsync(o.AssignedDriverId.Value);
            var dlg = new TtnDialog(o ?? order, driver) { Owner = Window.GetWindow(this) };
            dlg.ShowDialog();
        }
        catch (Exception ex) { MessageBox.Show($"Ошибка: {ex.Message}"); }
    }

    // ─── Подтвердить погрузку ────────────────────────────────────────────────────
    private async void ConfirmLoading_Click(object sender, RoutedEventArgs e)
    {
        if (OrdersGrid.SelectedItem is not Order selected) return;
        if (MessageBox.Show($"Подтвердить погрузку ТС для заказа {selected.OrderNumber}?",
            "Подтверждение погрузки", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        try
        {
            using var scope = App.Services.CreateScope();
            var repo  = scope.ServiceProvider.GetRequiredService<IOrderRepository>();
            var order = await repo.GetByIdAsync(selected.Id);
            if (order == null) return;
            order.Status           = OrderStatus.Loading;
            order.LoadingStartedAt = DateTime.Now;
            repo.Update(order);
            await repo.SaveChangesAsync();
            await LoadOrdersAsync();
        }
        catch (Exception ex) { MessageBox.Show($"Ошибка: {ex.Message}"); }
    }

    // ─── Импорт CSV ──────────────────────────────────────────────────────────────
    private async void ImportCsv_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new ImportCsvDialog { Owner = Window.GetWindow(this) };
        if (dlg.ShowDialog() == true)
        {
            MessageBox.Show($"Импортировано {dlg.ImportedCount} заказов.",
                "Импорт завершён", MessageBoxButton.OK, MessageBoxImage.Information);
            await LoadOrdersAsync();
        }
    }

    // ─── Веб-заявки ──────────────────────────────────────────────────────────────
    private async void WebServer_Click(object sender, RoutedEventArgs e)
    {
        var server = App.Services.GetRequiredService<WebOrderServer>();
        var dlg    = new WebServerDialog(server) { Owner = Window.GetWindow(this) };
        dlg.ShowDialog();
        await LoadOrdersAsync();
    }
}
