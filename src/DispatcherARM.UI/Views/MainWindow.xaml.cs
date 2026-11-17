using System.Windows;
using System.Windows.Controls;
using DispatcherARM.Domain.Entities;
using DispatcherARM.Domain.Enums;
using DispatcherARM.Domain.Interfaces;
using DispatcherARM.UI.Views.Dialogs;
using DispatcherARM.UI.Views.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace DispatcherARM.UI.Views;

public partial class MainWindow : Window
{
    private readonly User _currentUser;

    public MainWindow(User currentUser)
    {
        InitializeComponent();
        _currentUser = currentUser;

        UserNameText.Text = currentUser.FullName;
        UserRoleText.Text = currentUser.Role switch
        {
            UserRole.Admin      => "[Администратор]",
            UserRole.Dispatcher => "[Диспетчер]",
            UserRole.Driver     => "[Водитель]",
            _                   => ""
        };

        ApplyRoleRestrictions();
        MenuList.SelectedIndex = 0;
        _ = UpdateOrderCountAsync();
    }

    private void ApplyRoleRestrictions()
    {
        if (_currentUser.Role == UserRole.Driver)
        {
            // Водитель: Дашборд, Мои заказы, Карта, Кошелёк, Поддержка
            MenuOrders.Visibility       = Visibility.Collapsed;
            MenuDriverOrders.Visibility = Visibility.Visible;
            MenuDrivers.Visibility      = Visibility.Collapsed;
            MenuVehicles.Visibility     = Visibility.Collapsed;
            MenuReports.Visibility      = Visibility.Collapsed;
            MenuSettings.Visibility     = Visibility.Collapsed;
            MenuRoutes.Visibility       = Visibility.Collapsed;
            MenuAdminUsers.Visibility   = Visibility.Collapsed;
            MenuWallet.Visibility       = Visibility.Visible;
            MenuIntegration.Visibility  = Visibility.Collapsed;
            MenuClientPortal.Visibility = Visibility.Collapsed;
        }
        else if (_currentUser.Role == UserRole.Admin)
        {
            MenuDriverOrders.Visibility = Visibility.Collapsed;
            MenuAdminUsers.Visibility   = Visibility.Visible;
            MenuWallet.Visibility       = Visibility.Collapsed;
        }
        else // Dispatcher
        {
            MenuDriverOrders.Visibility = Visibility.Collapsed;
            MenuAdminUsers.Visibility   = Visibility.Collapsed;
            MenuWallet.Visibility       = Visibility.Collapsed;
        }
    }

    private void MenuList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count == 0) return;
        if (e.AddedItems[0] is not ListBoxItem selectedItem) return;

        // «Поддержка» открывается как отдельное окно
        if (selectedItem.Name == "MenuSupport")
        {
            var bot = new HelpBotWindow { Owner = this };
            bot.Show();
            // Снять выделение со списка поддержки
            if (sender is ListBox lb) lb.SelectedItem = null;
            return;
        }

        UserControl? page = selectedItem.Name switch
        {
            "MenuDashboard"    => new DashboardPage(),
            "MenuOrders"       => new OrdersPage(),
            "MenuDriverOrders" => new DriverOrdersPage(_currentUser.Id),
            "MenuMap"          => new MapPage(),
            "MenuRoutes"       => new RoutesPage(),
            "MenuDrivers"      => new DriversPage(),
            "MenuVehicles"     => new VehiclesPage(),
            "MenuReports"      => new ReportsPage(),
            "MenuWallet"       => new WalletPage(_currentUser.Id),
            "MenuAdminUsers"   => new AdminUsersPage(),
            "MenuSettings"     => new SettingsPage(),
            "MenuIntegration"  => new IntegrationPage(),
            "MenuClientPortal" => new ClientPortalPage(),
            _                  => null
        };

        if (page != null)
            ContentArea.Content = page;
    }

    // ─── Калькулятор ─────────────────────────────────────────────────────────────
    private void Calculator_Click(object sender, RoutedEventArgs e)
    {
        var calc = new RouteCostCalculator { Owner = this };
        calc.Show();
    }

    // ─── Смена пароля ────────────────────────────────────────────────────────────
    private void ChangePassword_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new ChangePasswordDialog(_currentUser.Id, _currentUser.Login) { Owner = this };
        dlg.ShowDialog();
    }

    private async Task UpdateOrderCountAsync()
    {
        try
        {
            using var scope = App.Services.CreateScope();
            var repo   = scope.ServiceProvider.GetRequiredService<IOrderRepository>();
            var orders = await repo.GetAllAsync();
            OrderCountText.Text = $"Заказов: {orders.Count()}";
        }
        catch { OrderCountText.Text = "Заказов: —"; }
    }

    private void LogoutButton_Click(object sender, RoutedEventArgs e)
    {
        new LoginView().Show();
        Close();
    }
}
