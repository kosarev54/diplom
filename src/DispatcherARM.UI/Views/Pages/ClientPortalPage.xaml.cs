using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DispatcherARM.Domain.Entities;
using DispatcherARM.Domain.Enums;
using DispatcherARM.Domain.Interfaces;
using DispatcherARM.UI.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DispatcherARM.UI.Views.Pages;

public partial class ClientPortalPage : UserControl
{
    private readonly WebOrderServer _server;
    private int _webOrdersCount;

    public ClientPortalPage()
    {
        InitializeComponent();
        _server = App.Services.GetRequiredService<WebOrderServer>();
        _server.OrderReceived += OnOrderReceived;
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateStatus();
        // Попытаться загрузить WebView2 — встроенный браузер с формой
        TryLoadWebView();
    }

    private void TryLoadWebView()
    {
        try
        {
            if (_server.IsRunning)
            {
                WebView.Source     = new Uri(WebOrderServer.Url);
                WebView.Visibility = Visibility.Visible;
                FallbackPanel.Visibility = Visibility.Collapsed;
            }
        }
        catch
        {
            // WebView2 Runtime не установлен — используем встроенную WPF форму
            WebView.Visibility       = Visibility.Collapsed;
            FallbackPanel.Visibility = Visibility.Visible;
        }
    }

    // ── Сервер ────────────────────────────────────────────────────────────────

    private void StartServer_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _server.Start();
            UpdateStatus();
            TryLoadWebView();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка запуска сервера:\n{ex.Message}\n\nПроверьте, что порт 5050 не занят.",
                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void StopServer_Click(object sender, RoutedEventArgs e)
    {
        _server.Stop();
        UpdateStatus();
        WebView.Visibility       = Visibility.Collapsed;
        FallbackPanel.Visibility = Visibility.Visible;
    }

    private async void RefreshOrders_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            using var scope = App.Services.CreateScope();
            var repo   = scope.ServiceProvider.GetRequiredService<IOrderRepository>();
            var orders = (await repo.GetAllAsync())
                .Where(o => o.Source == OrderSource.Web)
                .OrderByDescending(o => o.CreatedAt)
                .Take(3)
                .ToList();

            var webCount = (await repo.GetAllAsync()).Count(o => o.Source == OrderSource.Web);
            NewOrdersBadge.Text = $"{webCount} веб-заявок";

            if (orders.Any())
            {
                RecentOrderText.Text = string.Join(" | ",
                    orders.Select(o => $"{o.OrderNumber} — {o.ClientName}"));
            }
        }
        catch (Exception ex) { RecentOrderText.Text = $"Ошибка: {ex.Message}"; }
    }

    private void UpdateStatus()
    {
        bool running = _server.IsRunning;
        StatusDot.Fill  = new SolidColorBrush(running ? Color.FromRgb(34, 197, 94) : Color.FromRgb(239, 68, 68));
        StatusText.Text = running
            ? $"✅ Сервер работает — {WebOrderServer.Url} | Клиенты могут отправлять заявки"
            : "⏹ Сервер остановлен — нажмите «Запустить» для приёма заявок";
        StartBtn.IsEnabled = !running;
        StopBtn.IsEnabled  =  running;
    }

    private void OnOrderReceived(string orderNumber)
    {
        Dispatcher.Invoke(() =>
        {
            _webOrdersCount++;
            NewOrdersBadge.Text  = $"{_webOrdersCount} новых";
            RecentOrderText.Text = $"Последняя: {orderNumber} в {DateTime.Now:HH:mm:ss}";
        });
    }

    // ── Встроенная форма (Fallback без WebView2) ──────────────────────────────

    private async void SubmitOrder_Click(object sender, RoutedEventArgs e)
    {
        WErrorText.Visibility   = Visibility.Collapsed;
        WSuccessText.Visibility = Visibility.Collapsed;

        var clientName = WClientName.Text.Trim();
        var address    = WAddress.Text.Trim();
        if (string.IsNullOrEmpty(clientName)) { ShowErr("Укажите имя / организацию клиента"); return; }
        if (string.IsNullOrEmpty(address))    { ShowErr("Укажите адрес доставки"); return; }

        double.TryParse(WWeight.Text, CultureInfo.InvariantCulture, out double weight);
        double.TryParse(WVolume.Text, CultureInfo.InvariantCulture, out double volume);
        decimal.TryParse(WPrice.Text, CultureInfo.InvariantCulture, out decimal price);

        var priority = (OrderPriority)(WPriority.SelectedIndex < 0 ? 1 : WPriority.SelectedIndex);

        try
        {
            using var scope = App.Services.CreateScope();
            var repo   = scope.ServiceProvider.GetRequiredService<IOrderRepository>();
            var orders = await repo.GetAllAsync();
            var date   = DeliveryDateCalculator.Calculate(
                AppSettings.Instance.DepotLat, AppSettings.Instance.DepotLon, priority, orders);

            var order = new Order
            {
                OrderNumber   = $"ORD-PORTAL-{DateTime.Now:yyyyMMddHHmmss}",
                Source        = OrderSource.Web,
                ClientName    = clientName,
                ClientPhone   = WClientPhone.Text.Trim(),
                Address       = address,
                Latitude      = AppSettings.Instance.DepotLat,
                Longitude     = AppSettings.Instance.DepotLon,
                Weight        = weight,
                Volume        = volume,
                DeliveryPrice = price,
                Priority      = priority,
                Notes         = WNotes.Text.Trim().Length > 0 ? $"Портал: {WNotes.Text.Trim()}" : "Заявка через клиентский портал",
                DeliveryDate  = date,
                Status        = OrderStatus.New
            };

            await repo.AddAsync(order);
            await repo.SaveChangesAsync();

            // Сброс формы
            WClientName.Text = WClientPhone.Text = WAddress.Text = "";
            WWeight.Text = WVolume.Text = WPrice.Text = WNotes.Text = "";
            WPriority.SelectedIndex = 1;
            _webOrdersCount++;
            NewOrdersBadge.Text  = $"{_webOrdersCount} новых";
            RecentOrderText.Text = $"Последняя: {order.OrderNumber} от {clientName}";

            WSuccessText.Text       = $"✅ Заявка {order.OrderNumber} принята! Расчётная дата доставки: {date:dd.MM.yyyy}. Диспетчер назначит водителя и свяжется с вами.";
            WSuccessText.Visibility = Visibility.Visible;
        }
        catch (Exception ex) { ShowErr($"Ошибка: {ex.Message}"); }
    }

    private void ShowErr(string msg)
    {
        WErrorText.Text       = msg;
        WErrorText.Visibility = Visibility.Visible;
    }
}
