using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DispatcherARM.Domain.Entities;
using DispatcherARM.Domain.Interfaces;
using DispatcherARM.UI.Views.Cards;
using Microsoft.Extensions.DependencyInjection;

namespace DispatcherARM.UI.Views.Pages;

public partial class RoutesPage : UserControl
{
    public RoutesPage() { InitializeComponent(); }

    private async void RoutesPage_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadRoutesAsync();
    }

    private async Task LoadRoutesAsync()
    {
        try
        {
            using var scope = App.Services.CreateScope();
            var routeRepo = scope.ServiceProvider.GetRequiredService<IRouteRepository>();
            var date = DateFilter.SelectedDate ?? DateTime.Today;
            var routes = await routeRepo.GetRoutesByDateAsync(date);
            RoutesGrid.ItemsSource = routes;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка загрузки маршрутов: {ex.Message}");
        }
    }

    private async void FilterByDate_Click(object sender, RoutedEventArgs e) => await LoadRoutesAsync();

    private void RoutesGrid_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (RoutesGrid.SelectedItem is Route route)
        {
            var card = new RouteCard(route.Id);
            card.Owner = Window.GetWindow(this);
            card.ShowDialog();
        }
    }

    private async void BuildRoute_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new DispatcherARM.UI.Views.Dialogs.BuildRouteDialog();
        dialog.Owner = Window.GetWindow(this);
        if (dialog.ShowDialog() == true)
        {
            await LoadRoutesAsync();
        }
    }
}
