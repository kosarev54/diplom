using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DispatcherARM.Domain.Entities;
using DispatcherARM.Domain.Interfaces;
using DispatcherARM.UI.Views.Cards;
using Microsoft.Extensions.DependencyInjection;

namespace DispatcherARM.UI.Views.Pages;

public partial class DriversPage : UserControl
{
    public DriversPage() { InitializeComponent(); }

    private async void DriversPage_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            using var scope = App.Services.CreateScope();
            var driverRepo = scope.ServiceProvider.GetRequiredService<IDriverRepository>();
            DriversGrid.ItemsSource = await driverRepo.GetAllAsync();
        }
        catch (Exception ex) { MessageBox.Show($"Ошибка: {ex.Message}"); }
    }

    private void DriversGrid_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DriversGrid.SelectedItem is Driver driver)
        {
            var card = new DriverCard(driver.Id);
            card.Owner = Window.GetWindow(this);
            card.ShowDialog();
        }
    }

    private void PaymentCalc_Click(object sender, RoutedEventArgs e)
    {
        if (DriversGrid.SelectedItem is not Driver driver)
        {
            MessageBox.Show("Выберите водителя для расчёта зарплаты.", "Нет выбора");
            return;
        }
        var dlg = new DispatcherARM.UI.Views.Dialogs.DriverPaymentDialog(driver)
        {
            Owner = Window.GetWindow(this)
        };
        dlg.ShowDialog();
    }

    private async void AddDriver_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new DispatcherARM.UI.Views.Dialogs.DriverDialog();
        dialog.Owner = Window.GetWindow(this);
        if (dialog.ShowDialog() == true)
        {
            using var scope = App.Services.CreateScope();
            var driverRepo = scope.ServiceProvider.GetRequiredService<IDriverRepository>();
            DriversGrid.ItemsSource = await driverRepo.GetAllAsync();
        }
    }
    private async void EditDriver_Click(object sender, RoutedEventArgs e)
    {
        if (DriversGrid.SelectedItem is not Driver driver)
        {
            MessageBox.Show("Выберите водителя для редактирования.", "Не выбрано");
            return;
        }
        var dialog = new DispatcherARM.UI.Views.Dialogs.DriverDialog(driver)
        {
            Owner = Window.GetWindow(this)
        };
        if (dialog.ShowDialog() == true)
        {
            using var scope = App.Services.CreateScope();
            var driverRepo = scope.ServiceProvider.GetRequiredService<IDriverRepository>();
            DriversGrid.ItemsSource = await driverRepo.GetAllAsync();
        }
    }

    private async void DeleteDriver_Click(object sender, RoutedEventArgs e)
    {
        if (DriversGrid.SelectedItem is not Driver driver)
        {
            MessageBox.Show("Выберите водителя для удаления.", "Не выбрано");
            return;
        }
        var result = MessageBox.Show(
            $"Удалить водителя «{driver.FullName}»?\n\nЭто действие нельзя отменить.",
            "Подтверждение удаления",
            MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes) return;
        try
        {
            using var scope = App.Services.CreateScope();
            var driverRepo = scope.ServiceProvider.GetRequiredService<IDriverRepository>();
            // Delete принимает объект, не id
            driverRepo.Delete(driver);
            await driverRepo.SaveChangesAsync();
            DriversGrid.ItemsSource = await driverRepo.GetAllAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка удаления: {ex.Message}");
        }
    }
}
