using System.IO;
using System.Windows;
using System.Windows.Controls;
using DispatcherARM.UI.Services;

namespace DispatcherARM.UI.Views.Pages;

/// <summary>
/// Страница настроек — читает и сохраняет AppSettings
/// </summary>
public partial class SettingsPage : UserControl
{
    public SettingsPage()
    {
        InitializeComponent();
        Loaded += SettingsPage_Loaded;
    }

    private void SettingsPage_Loaded(object sender, RoutedEventArgs e)
        => PopulateFields(AppSettings.Instance);

    private void PopulateFields(AppSettings s)
    {
        PublicUrlBox.Text        = s.OsrmPublicUrl;
        LocalUrlBox.Text         = s.OsrmLocalUrl;
        UseLocalOsrm.IsChecked   = s.UseLocalOsrm;
        DepotLatBox.Text         = s.DepotLat.ToString("F6", System.Globalization.CultureInfo.InvariantCulture);
        DepotLonBox.Text         = s.DepotLon.ToString("F6", System.Globalization.CultureInfo.InvariantCulture);
        DepotAddressBox.Text     = s.DepotAddress;
        OrgNameBox.Text          = s.OrganizationName;
        OrgInnBox.Text           = s.OrganizationInn;
        ExchangeFolderBox.Text   = s.OneCExchangeFolder;
        WebPortBox.Text          = s.WebServerPort.ToString();
        FuelPriceBox.Text        = s.FuelPricePerLiter.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
        CommissionBox.Text       = (s.DriverCommissionRate * 100).ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
    }

    private void SaveOsrmSettings_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var s = AppSettings.Instance;

            s.OsrmPublicUrl = PublicUrlBox.Text.Trim();
            s.OsrmLocalUrl  = LocalUrlBox.Text.Trim();
            s.UseLocalOsrm  = UseLocalOsrm.IsChecked == true;

            if (double.TryParse(DepotLatBox.Text,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var lat))
                s.DepotLat = lat;

            if (double.TryParse(DepotLonBox.Text,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var lon))
                s.DepotLon = lon;

            s.DepotAddress       = DepotAddressBox.Text.Trim();
            s.OrganizationName   = OrgNameBox.Text.Trim();
            s.OrganizationInn    = OrgInnBox.Text.Trim();
            s.OneCExchangeFolder = ExchangeFolderBox.Text.Trim();

            if (int.TryParse(WebPortBox.Text, out var port) && port is > 1024 and < 65535)
                s.WebServerPort = port;

            if (double.TryParse(FuelPriceBox.Text,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var fuel))
                s.FuelPricePerLiter = fuel;

            if (double.TryParse(CommissionBox.Text,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var comm))
                s.DriverCommissionRate = comm / 100.0;

            s.Save();

            MessageBox.Show(
                "Настройки сохранены!\n\nИзменения координат депо и ставок вступят в силу немедленно.",
                "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка сохранения: {ex.Message}", "Ошибка",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ResetDefaults_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("Сбросить настройки к значениям по умолчанию?",
                "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        var fresh = new AppSettings();
        fresh.Save();
        PopulateFields(fresh);
        MessageBox.Show("Настройки сброшены.", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void RefreshLog_Click(object sender, RoutedEventArgs e)
    {
        var lines = AppLogger.ReadLastLines(150);
        LogViewBox.Text = string.Join(Environment.NewLine, lines);
        LogViewBox.ScrollToEnd();
    }

    private void OpenLogFolder_Click(object sender, RoutedEventArgs e)
    {
        var folder = Path.Combine(AppSettings.AppDataDir, "logs");
        Directory.CreateDirectory(folder);
        System.Diagnostics.Process.Start("explorer.exe", folder);
    }
}
