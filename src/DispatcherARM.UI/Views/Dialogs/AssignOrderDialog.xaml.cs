using System.Windows;
using System.Windows.Controls;
using DispatcherARM.Domain.Entities;
using DispatcherARM.Domain.Enums;
using DispatcherARM.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace DispatcherARM.UI.Views.Dialogs;

public class DriverSelectItem
{
    public int    Id          { get; set; }
    public string DisplayText { get; set; } = "";
    public Driver Driver      { get; set; } = null!;
}

public class VehicleSelectItem
{
    public int     Id          { get; set; }
    public string  DisplayText { get; set; } = "";
    public Vehicle Vehicle     { get; set; } = null!;
}

public partial class AssignOrderDialog : Window
{
    private readonly Order _order;
    public int? SelectedDriverId { get; private set; }
    public int? SelectedVehicleId { get; private set; }
    public DateTime? SelectedDeliveryDate { get; private set; }

    public AssignOrderDialog(Order order)
    {
        InitializeComponent();
        _order = order;
        OrderInfoText.Text = $"Заказ {order.OrderNumber} · {order.ClientName} · {order.Address}";
        DeliveryDatePicker.SelectedDate = order.DeliveryDate ?? DateTime.Today;
        Loaded += async (_, _) => await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        using var scope = App.Services.CreateScope();
        var driverRepo  = scope.ServiceProvider.GetRequiredService<IDriverRepository>();
        var vehicleRepo = scope.ServiceProvider.GetRequiredService<IVehicleRepository>();

        // GetAvailableDriversAsync уже делает Include(AssignedVehicle)
        var allDrivers = (await driverRepo.GetAvailableDriversAsync()).ToList();
        // Добавляем всех остальных водителей (In Route и т.д.) которых тоже можно назначить
        var availableIds = allDrivers.Select(d => d.Id).ToHashSet();
        var otherDrivers = (await driverRepo.GetAllAsync())
            .Where(d => !availableIds.Contains(d.Id)).ToList();
        var drivers  = allDrivers.Concat(otherDrivers).ToList();
        var vehicles = (await vehicleRepo.GetAllAsync()).ToList();

        // Водители — все доступные
        var driverItems = drivers
            .Select(d => new DriverSelectItem
            {
                Id = d.Id,
                DisplayText = $"{d.FullName}  [{d.Status switch {
                    DriverStatus.Available => "✅ Доступен",
                    DriverStatus.OnRoute   => "🚗 В рейсе",
                    DriverStatus.OnLeave   => "🏖 В отпуске",
                    _                      => "❌ Отстранён"
                }}]  · {d.WorkSchedule ?? "—"}",
                Driver = d
            }).ToList();

        // Если у заказа уже назначен водитель — показываем его первым
        if (_order.AssignedDriverId.HasValue)
        {
            var existing = drivers.FirstOrDefault(d => d.Id == _order.AssignedDriverId);
            if (existing != null && driverItems.All(i => i.Id != existing.Id))
                driverItems.Insert(0, new DriverSelectItem
                {
                    Id = existing.Id,
                    DisplayText = $"[Текущий] {existing.FullName}",
                    Driver = existing
                });
        }

        DriverBox.ItemsSource = driverItems;
        if (_order.AssignedDriverId.HasValue)
            DriverBox.SelectedItem = driverItems.FirstOrDefault(i => i.Id == _order.AssignedDriverId);

        // ТС — все
        var vehicleItems = vehicles.Select(v => new VehicleSelectItem
        {
            Id = v.Id,
            DisplayText = $"{v.LicensePlate} — {v.Model} ({v.Type}, {v.MaxWeightKg} кг)",
            Vehicle = v
        }).ToList();

        VehicleBox.ItemsSource = vehicleItems;

        // Если у водителя есть ТС — подставляем автоматически
        if (_order.AssignedDriverId.HasValue)
        {
            var drv = drivers.FirstOrDefault(d => d.Id == _order.AssignedDriverId);
            if (drv?.AssignedVehicleId.HasValue == true)
                VehicleBox.SelectedItem = vehicleItems.FirstOrDefault(v => v.Id == drv.AssignedVehicleId);
        }
    }

    private void DriverBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DriverBox.SelectedItem is not DriverSelectItem item)
        {
            DriverInfoBorder.Visibility = Visibility.Collapsed;
            return;
        }
        var d = item.Driver;
        DriverInfoBorder.Visibility = Visibility.Visible;
        DriverPhoneText.Text    = d.Phone;
        DriverScheduleText.Text = d.WorkSchedule ?? "—";
        DriverStatusText.Text   = d.Status switch
        {
            DriverStatus.Available => "✅ Доступен",
            DriverStatus.OnRoute   => "🚗 В рейсе",
            DriverStatus.OnLeave   => "🏖 В отпуске",
            _                      => "❌ Отстранён"
        };
        DriverVehicleText.Text = d.AssignedVehicle != null
            ? $"{d.AssignedVehicle.LicensePlate} {d.AssignedVehicle.Model}"
            : "Не назначено";

        // Автоподставляем ТС водителя
        if (d.AssignedVehicleId.HasValue && VehicleBox.Items.Count > 0)
        {
            foreach (VehicleSelectItem vi in VehicleBox.Items)
            {
                if (vi.Id == d.AssignedVehicleId)
                { VehicleBox.SelectedItem = vi; break; }
            }
        }
    }

    private void Assign_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Visibility = Visibility.Collapsed;
        if (DriverBox.SelectedItem is not DriverSelectItem di)
        { ShowError("Выберите водителя"); return; }
        if (VehicleBox.SelectedItem is not VehicleSelectItem vi)
        { ShowError("Выберите транспортное средство"); return; }

        SelectedDriverId      = di.Id;
        SelectedVehicleId     = vi.Id;
        SelectedDeliveryDate  = DeliveryDatePicker.SelectedDate ?? DateTime.Today;
        DialogResult = true;
        Close();
    }

    private void ShowError(string msg)
    {
        ErrorText.Text = msg;
        ErrorText.Visibility = Visibility.Visible;
    }
}
