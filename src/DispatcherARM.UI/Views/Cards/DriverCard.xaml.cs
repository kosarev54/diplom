using System.Windows;
using System.Windows.Media;
using DispatcherARM.Domain.Entities;
using DispatcherARM.Domain.Enums;
using DispatcherARM.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace DispatcherARM.UI.Views.Cards;

/// <summary>
/// Карточка водителя с детальной информацией
/// </summary>
public partial class DriverCard : Window
{
    private readonly int _driverId;
    private Driver? _driver;

    public DriverCard(int driverId)
    {
        InitializeComponent();
        _driverId = driverId;
    }




    /// <summary>Построить календарь-сетку текущего месяца</summary>
    private void BuildCalendar(List<Route> monthRoutes)
    {
        var now = DateTime.Now;
        var daysInMonth = DateTime.DaysInMonth(now.Year, now.Month);
        var routeDates = monthRoutes.Select(r => r.Date.Date).ToHashSet();

        // Рассчитываем рабочие дни по графику (5/2 — пн-пт рабочие)
        var schedule = _driver?.WorkSchedule ?? "5/2";
        var items = new List<CalendarDay>();
        int workDays = 0, offDays = 0;

        for (int day = 1; day <= daysInMonth; day++)
        {
            var date = new DateTime(now.Year, now.Month, day);
            bool isWeekend = date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday;
            bool isRoute = routeDates.Contains(date.Date);
            bool isFuture = date > now.Date;

            string color;
            string tooltip;

            if (isRoute)
            {
                color = "#1976D2";
                tooltip = $"{day} — В рейсе";
                workDays++;
            }
            else if (isFuture)
            {
                color = "#9E9E9E";
                tooltip = $"{day} — Запланировано";
            }
            else if (isWeekend || (schedule == "5/2" && isWeekend))
            {
                color = "#E53935";
                tooltip = $"{day} — Выходной";
                offDays++;
            }
            else
            {
                color = "#43A047";
                tooltip = $"{day} — Рабочий";
                workDays++;
            }

            items.Add(new CalendarDay
            {
                Day = day.ToString(),
                DayOfWeek = date.DayOfWeek.ToString()[..2].ToUpper(),
                DayColor = color,
                Tooltip = tooltip
            });
        }

        CalendarGrid.ItemsSource = items;
        WorkDaysText.Text = workDays.ToString();
        OffDaysText.Text = offDays.ToString();
    }

    private async void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (_driver == null) return;
        var dlg = new DispatcherARM.UI.Views.Dialogs.DriverDialog(_driver)
        {
            Owner = this
        };
        if (dlg.ShowDialog() == true)
        {
            // Перезагружаем данные в карточке
            await DriverCard_LoadedAsync();
        }
    }

    // Вынесли логику загрузки в отдельный метод для переиспользования
    private async void DriverCard_Loaded(object sender, RoutedEventArgs e)
        => await DriverCard_LoadedAsync();

    private async Task DriverCard_LoadedAsync()
    {
        try
        {
            using var scope = App.Services.CreateScope();
            var driverRepo = scope.ServiceProvider.GetRequiredService<IDriverRepository>();
            var routeRepo  = scope.ServiceProvider.GetRequiredService<IRouteRepository>();

            _driver = await driverRepo.GetDriverWithRoutesAsync(_driverId);
            if (_driver == null) { Close(); return; }

            DriverNameText.Text = _driver.FullName;
            PhoneText.Text      = _driver.Phone;
            VehicleText.Text    = _driver.AssignedVehicle != null
                ? $"ТС: {_driver.AssignedVehicle.LicensePlate} {_driver.AssignedVehicle.Model}"
                : "Без ТС";

            switch (_driver.Status)
            {
                case DriverStatus.Available:
                    StatusText.Text = "Доступен";
                    StatusIcon.Foreground = new SolidColorBrush(Colors.LightGreen); break;
                case DriverStatus.OnRoute:
                    StatusText.Text = "В рейсе";
                    StatusIcon.Foreground = new SolidColorBrush(Colors.Orange); break;
                case DriverStatus.OnLeave:
                    StatusText.Text = "В отпуске";
                    StatusIcon.Foreground = new SolidColorBrush(Colors.CornflowerBlue); break;
                default:
                    StatusText.Text = "Отстранён";
                    StatusIcon.Foreground = new SolidColorBrush(Colors.Red); break;
            }

            var routes = _driver.Routes.ToList();
            TotalRoutesText.Text = routes.Count.ToString();
            TotalKmText.Text     = routes.Sum(r => r.TotalDistanceKm).ToString("F0");
            SalaryText.Text      = $"{_driver.BaseSalary:N0} ₽";

            InfoName.Text         = _driver.FullName;
            InfoPhone.Text        = _driver.Phone;
            InfoEmail.Text        = _driver.Email ?? "—";
            InfoBirth.Text        = _driver.BirthDate?.ToString("dd.MM.yyyy") ?? "—";
            InfoAddress.Text      = _driver.Address ?? "—";
            InfoHireDate.Text     = _driver.HireDate?.ToString("dd.MM.yyyy") ?? "—";
            InfoLicense.Text      = _driver.LicenseNumber;
            InfoCategories.Text   = _driver.LicenseCategory ?? "—";
            InfoLicenseExpiry.Text = _driver.LicenseExpiryDate?.ToString("dd.MM.yyyy") ?? "—";
            InfoVehicle.Text      = _driver.AssignedVehicle != null
                ? $"{_driver.AssignedVehicle.LicensePlate} — {_driver.AssignedVehicle.Model}"
                : "Не назначено";
            InfoSchedule.Text = _driver.WorkSchedule ?? "—";
            InfoNotes.Text    = _driver.Notes ?? "—";

            SalaryBaseText.Text   = $"{_driver.BaseSalary:N0} ₽";
            RateKmText.Text       = $"{_driver.RatePerKm:N0} ₽/км";
            RateDeliveryText.Text = $"{_driver.RatePerDelivery:N0} ₽";

            var now = DateTime.Now;
            var monthRoutes    = routes.Where(r => r.Date.Month == now.Month && r.Date.Year == now.Year).ToList();
            var monthDeliveries = monthRoutes.Sum(r => r.Waypoints?.Count ?? 0);
            var monthKm        = monthRoutes.Sum(r => r.TotalDistanceKm);
            var estimated      = _driver.BaseSalary + (decimal)(monthKm * (double)_driver.RatePerKm)
                               + (decimal)(monthDeliveries * (double)_driver.RatePerDelivery);
            EstimatedMonthText.Text = $"{estimated:N0} ₽";

            var detailedRoutes = new List<Route>();
            foreach (var r in routes.OrderByDescending(r => r.Date).Take(50))
            {
                var detailed = await routeRepo.GetRouteWithWaypointsAsync(r.Id);
                if (detailed != null) detailedRoutes.Add(detailed);
            }
            RoutesGrid.ItemsSource = detailedRoutes;
            MonthKmText.Text = $"{monthKm:F0} км";
            BuildCalendar(monthRoutes);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка загрузки: {ex.Message}");
        }
    }
}

/// <summary>День для календаря-сетки</summary>
public class CalendarDay
{
    public string Day { get; set; } = "";
    public string DayOfWeek { get; set; } = "";
    public string DayColor { get; set; } = "#9E9E9E";
    public string Tooltip { get; set; } = "";
}
