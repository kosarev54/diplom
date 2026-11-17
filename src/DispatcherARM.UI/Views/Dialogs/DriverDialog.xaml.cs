using System.Windows;
using DispatcherARM.Domain.Entities;
using DispatcherARM.Domain.Enums;
using DispatcherARM.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace DispatcherARM.UI.Views.Dialogs;

// ViewModel для отображения ТС в ComboBox
public class VehicleItem
{
    public int    Id          { get; set; }
    public string DisplayText { get; set; } = "";
}

public partial class DriverDialog : Window
{
    private readonly Driver? _editDriver; // null = создание нового
    private List<Vehicle> _vehicles = new();

    // Конструктор создания нового водителя
    public DriverDialog()
    {
        InitializeComponent();
        DialogTitle.Text = "Новый водитель";
        HireDatePicker.SelectedDate = DateTime.Today;
        Loaded += async (_, _) => await LoadVehiclesAsync(null);
    }

    // Конструктор редактирования существующего
    public DriverDialog(Driver driver)
    {
        InitializeComponent();
        _editDriver = driver;
        DialogTitle.Text = "Редактировать водителя";
        Loaded += async (_, _) =>
        {
            await LoadVehiclesAsync(driver.AssignedVehicleId);
            FillForm(driver);
        };
    }

    private async Task LoadVehiclesAsync(int? selectedId)
    {
        using var scope = App.Services.CreateScope();
        var vehicleRepo = scope.ServiceProvider.GetRequiredService<IVehicleRepository>();
        _vehicles = (await vehicleRepo.GetAllAsync()).ToList();

        // Добавляем «Без ТС»
        var items = new List<VehicleItem>
        {
            new() { Id = 0, DisplayText = "— Без ТС —" }
        };
        items.AddRange(_vehicles.Select(v => new VehicleItem
        {
            Id = v.Id,
            DisplayText = $"{v.LicensePlate} — {v.Model}"
        }));

        VehicleBox.ItemsSource = items;
        VehicleBox.SelectedItem = items.FirstOrDefault(i => i.Id == (selectedId ?? 0))
                                  ?? items.First();
    }

    private void FillForm(Driver d)
    {
        FullNameBox.Text        = d.FullName;
        PhoneBox.Text           = d.Phone;
        EmailBox.Text           = d.Email ?? "";
        BirthDatePicker.SelectedDate = d.BirthDate;
        AddressBox.Text         = d.Address ?? "";
        LicenseBox.Text         = d.LicenseNumber;
        LicenseCatBox.Text      = d.LicenseCategory ?? "";
        LicenseExpiryPicker.SelectedDate = d.LicenseExpiryDate;
        HireDatePicker.SelectedDate      = d.HireDate;
        SalaryBox.Text          = d.BaseSalary.ToString("F0");
        RateKmBox.Text          = d.RatePerKm.ToString("F0");
        RateDelivBox.Text       = d.RatePerDelivery.ToString("F0");
        ScheduleBox.Text        = d.WorkSchedule ?? "";
        NotesBox.Text           = d.Notes ?? "";

        // Статус
        foreach (System.Windows.Controls.ComboBoxItem item in StatusBox.Items)
        {
            if (item.Tag?.ToString() == d.Status.ToString())
            {
                StatusBox.SelectedItem = item;
                break;
            }
        }
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Visibility = Visibility.Collapsed;

        // Валидация
        if (string.IsNullOrWhiteSpace(FullNameBox.Text))
        {
            ShowError("Введите ФИО водителя"); return;
        }
        if (string.IsNullOrWhiteSpace(PhoneBox.Text))
        {
            ShowError("Введите телефон"); return;
        }
        if (string.IsNullOrWhiteSpace(LicenseBox.Text))
        {
            ShowError("Введите номер водительского удостоверения"); return;
        }

        // Парсим числа
        if (!decimal.TryParse(SalaryBox.Text.Replace(",", "."), System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var salary))
            salary = 0;
        if (!decimal.TryParse(RateKmBox.Text.Replace(",", "."), System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var rateKm))
            rateKm = 0;
        if (!decimal.TryParse(RateDelivBox.Text.Replace(",", "."), System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var rateDeliv))
            rateDeliv = 0;

        // Статус
        var statusTag = (StatusBox.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Tag?.ToString() ?? "Available";
        var status = statusTag switch
        {
            "OnRoute"    => DriverStatus.OnRoute,
            "OnLeave"    => DriverStatus.OnLeave,
            "Suspended"  => DriverStatus.Suspended,
            _            => DriverStatus.Available
        };

        // ТС
        var selectedVehicle = VehicleBox.SelectedItem as VehicleItem;
        int? vehicleId = selectedVehicle?.Id > 0 ? selectedVehicle.Id : null;

        try
        {
            using var scope = App.Services.CreateScope();
            var driverRepo = scope.ServiceProvider.GetRequiredService<IDriverRepository>();

            if (_editDriver == null)
            {
                // СОЗДАНИЕ
                var driver = new Driver
                {
                    FullName          = FullNameBox.Text.Trim(),
                    Phone             = PhoneBox.Text.Trim(),
                    Email             = EmailBox.Text.Trim().NullIfEmpty(),
                    BirthDate         = BirthDatePicker.SelectedDate,
                    Address           = AddressBox.Text.Trim().NullIfEmpty(),
                    LicenseNumber     = LicenseBox.Text.Trim(),
                    LicenseCategory   = LicenseCatBox.Text.Trim().NullIfEmpty(),
                    LicenseExpiryDate = LicenseExpiryPicker.SelectedDate,
                    HireDate          = HireDatePicker.SelectedDate,
                    BaseSalary        = salary,
                    RatePerKm         = rateKm,
                    RatePerDelivery   = rateDeliv,
                    WorkSchedule      = ScheduleBox.Text.Trim().NullIfEmpty(),
                    Notes             = NotesBox.Text.Trim().NullIfEmpty(),
                    Status            = status,
                    AssignedVehicleId = vehicleId
                };
                await driverRepo.AddAsync(driver);
            }
            else
            {
                // РЕДАКТИРОВАНИЕ
                _editDriver.FullName          = FullNameBox.Text.Trim();
                _editDriver.Phone             = PhoneBox.Text.Trim();
                _editDriver.Email             = EmailBox.Text.Trim().NullIfEmpty();
                _editDriver.BirthDate         = BirthDatePicker.SelectedDate;
                _editDriver.Address           = AddressBox.Text.Trim().NullIfEmpty();
                _editDriver.LicenseNumber     = LicenseBox.Text.Trim();
                _editDriver.LicenseCategory   = LicenseCatBox.Text.Trim().NullIfEmpty();
                _editDriver.LicenseExpiryDate = LicenseExpiryPicker.SelectedDate;
                _editDriver.HireDate          = HireDatePicker.SelectedDate;
                _editDriver.BaseSalary        = salary;
                _editDriver.RatePerKm         = rateKm;
                _editDriver.RatePerDelivery   = rateDeliv;
                _editDriver.WorkSchedule      = ScheduleBox.Text.Trim().NullIfEmpty();
                _editDriver.Notes             = NotesBox.Text.Trim().NullIfEmpty();
                _editDriver.Status            = status;
                _editDriver.AssignedVehicleId = vehicleId;
                driverRepo.Update(_editDriver);  // синхронный Update
            }

            await driverRepo.SaveChangesAsync();
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            ShowError($"Ошибка при сохранении: {ex.Message}");
        }
    }

    private void ShowError(string msg)
    {
        ErrorText.Text = msg;
        ErrorText.Visibility = Visibility.Visible;
    }
}

internal static class StringExtensions
{
    public static string? NullIfEmpty(this string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s;
}
