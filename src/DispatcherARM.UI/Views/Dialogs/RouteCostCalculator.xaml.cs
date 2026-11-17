using System.Windows;
using System.Windows.Controls;

namespace DispatcherARM.UI.Views.Dialogs;

public partial class RouteCostCalculator : Window
{
    public RouteCostCalculator() { InitializeComponent(); Recalculate(null, null); }

    public RouteCostCalculator(double distanceKm, int deliveries, double fuelConsPer100) : this()
    {
        DistanceBox.Text   = distanceKm.ToString("F1");
        DeliveriesBox.Text = deliveries.ToString();
        FuelConsBox.Text   = fuelConsPer100.ToString("F1");
        Recalculate(null, null);
    }

    private void Recalculate(object? sender, TextChangedEventArgs? e)
    {
        if (!IsInitialized) return;
        try
        {
            double dist     = double.TryParse(DistanceBox.Text,     out var d)  ? d  : 0;
            int    delivs   = int.TryParse   (DeliveriesBox.Text,   out var n)  ? n  : 0;
            double fuelP    = double.TryParse(FuelPriceBox.Text,    out var fp) ? fp : 55;
            double fuelCons = double.TryParse(FuelConsBox.Text,     out var fc) ? fc : 12;
            double rateKm   = double.TryParse(DriverRateKmBox.Text, out var rk) ? rk : 2;
            double rateDel  = double.TryParse(DriverRateDelivBox.Text, out var rd) ? rd : 150;
            double overhead = double.TryParse(OverheadBox.Text,     out var oh) ? oh : 0;

            double fuelCost  = dist * fuelCons / 100.0 * fuelP;
            double laborCost = dist * rateKm + delivs * rateDel;
            double total     = (fuelCost + laborCost) * (1 + overhead / 100.0);

            FuelCostText.Text  = $"{fuelCost:N0} ₽";
            LaborCostText.Text = $"{laborCost:N0} ₽";
            TotalCostText.Text = $"{total:N0} ₽";
        }
        catch { /* игнорируем ошибки при вводе */ }
    }
}
