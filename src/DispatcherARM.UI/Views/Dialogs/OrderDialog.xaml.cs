using System.Globalization;
using System.Windows;
using DispatcherARM.Domain.Entities;
using DispatcherARM.Domain.Enums;
using DispatcherARM.Domain.Interfaces;
using DispatcherARM.UI.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DispatcherARM.UI.Views.Dialogs;

public partial class OrderDialog : Window
{
    private readonly Order? _existingOrder;
    public Order? ResultOrder { get; private set; }

    public OrderDialog()
    {
        InitializeComponent();
        DialogTitle.Text = "Новый заказ";
        DeliveryDatePicker.SelectedDate = DateTime.Today;
    }

    public OrderDialog(Order order) : this()
    {
        _existingOrder = order;
        DialogTitle.Text = $"Редактирование: {order.OrderNumber}";

        ClientNameBox.Text    = order.ClientName;
        ClientPhoneBox.Text   = order.ClientPhone;
        AddressBox.Text       = order.Address;
        LatitudeBox.Text      = order.Latitude.ToString(CultureInfo.InvariantCulture);
        LongitudeBox.Text     = order.Longitude.ToString(CultureInfo.InvariantCulture);
        WeightBox.Text        = order.Weight.ToString(CultureInfo.InvariantCulture);
        VolumeBox.Text        = order.Volume.ToString(CultureInfo.InvariantCulture);
        DeliveryPriceBox.Text    = order.DeliveryPrice.ToString(CultureInfo.InvariantCulture);
        CashOnDeliveryBox.Text   = order.CashOnDelivery.ToString(CultureInfo.InvariantCulture);
        PriorityBox.SelectedIndex = (int)order.Priority;
        DeliveryDatePicker.SelectedDate = order.DeliveryDate;
        NotesBox.Text = order.Notes;
    }

    // ─── Автоматический расчёт даты ──────────────────────────────────────────────
    private async void CalcDate_Click(object sender, RoutedEventArgs e)
    {
        if (!double.TryParse(LatitudeBox.Text, CultureInfo.InvariantCulture, out double lat) ||
            !double.TryParse(LongitudeBox.Text, CultureInfo.InvariantCulture, out double lon))
        {
            DateHintText.Text = "⚠️ Сначала введите координаты (широта и долгота)";
            return;
        }

        var priority = (OrderPriority)PriorityBox.SelectedIndex;

        try
        {
            using var scope = App.Services.CreateScope();
            var repo   = scope.ServiceProvider.GetRequiredService<IOrderRepository>();
            var orders = await repo.GetAllAsync();

            double dist = DeliveryDateCalculator.HaversineKm(
                AppSettings.Instance.DepotLat, AppSettings.Instance.DepotLon, lat, lon);
            DateTime date = DeliveryDateCalculator.Calculate(lat, lon, priority, orders);

            DeliveryDatePicker.SelectedDate = date;
            DateHintText.Text =
                $"📍 Расстояние от депо: {dist:F1} км  →  Расчётная дата доставки: {date:dd.MM.yyyy}";
        }
        catch (Exception ex)
        {
            DateHintText.Text = $"Ошибка расчёта: {ex.Message}";
        }
    }

    // ─── Сохранить ────────────────────────────────────────────────────────────────
    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Visibility = Visibility.Collapsed;

        if (string.IsNullOrWhiteSpace(ClientNameBox.Text))
        { ShowError("Укажите имя клиента"); return; }
        if (string.IsNullOrWhiteSpace(AddressBox.Text))
        { ShowError("Укажите адрес доставки"); return; }
        if (!double.TryParse(LatitudeBox.Text, CultureInfo.InvariantCulture, out double lat) || lat < -90 || lat > 90)
        { ShowError("Некорректная широта (пример: 55.7558)"); return; }
        if (!double.TryParse(LongitudeBox.Text, CultureInfo.InvariantCulture, out double lon) || lon < -180 || lon > 180)
        { ShowError("Некорректная долгота (пример: 37.6173)"); return; }

        double.TryParse(WeightBox.Text, CultureInfo.InvariantCulture, out double weight);
        double.TryParse(VolumeBox.Text, CultureInfo.InvariantCulture, out double volume);
        decimal.TryParse(DeliveryPriceBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal delivPrice);
        decimal.TryParse(CashOnDeliveryBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal cod);

        try
        {
            using var scope = App.Services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IOrderRepository>();

            if (_existingOrder != null)
            {
                var order = await repo.GetByIdAsync(_existingOrder.Id);
                if (order == null) { ShowError("Заказ не найден"); return; }
                order.ClientName     = ClientNameBox.Text.Trim();
                order.ClientPhone    = ClientPhoneBox.Text?.Trim();
                order.Address        = AddressBox.Text.Trim();
                order.Latitude       = lat;
                order.Longitude      = lon;
                order.Weight         = weight;
                order.Volume         = volume;
                order.DeliveryPrice  = delivPrice;
                order.CashOnDelivery = cod;
                order.Priority       = (OrderPriority)PriorityBox.SelectedIndex;
                order.DeliveryDate   = DeliveryDatePicker.SelectedDate;
                order.Notes          = NotesBox.Text?.Trim();
                repo.Update(order);
                await repo.SaveChangesAsync();
                ResultOrder = order;
            }
            else
            {
                var order = new Order
                {
                    OrderNumber    = $"ORD-{DateTime.Now:yyyyMMddHHmmss}",
                    ClientName     = ClientNameBox.Text.Trim(),
                    ClientPhone    = ClientPhoneBox.Text?.Trim(),
                    Address        = AddressBox.Text.Trim(),
                    Latitude       = lat,
                    Longitude      = lon,
                    Weight         = weight,
                    Volume         = volume,
                    DeliveryPrice  = delivPrice,
                    CashOnDelivery = cod,
                    Priority       = (OrderPriority)PriorityBox.SelectedIndex,
                    DeliveryDate   = DeliveryDatePicker.SelectedDate,
                    Notes          = NotesBox.Text?.Trim(),
                    Status         = OrderStatus.New
                };
                await repo.AddAsync(order);
                await repo.SaveChangesAsync();
                ResultOrder = order;
            }

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            ShowError($"Ошибка сохранения: {ex.Message}");
        }
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }
}
