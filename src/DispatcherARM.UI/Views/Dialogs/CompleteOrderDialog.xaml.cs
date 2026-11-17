using System.Windows;
using DispatcherARM.Domain.Entities;
using DispatcherARM.Domain.Enums;
using DispatcherARM.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace DispatcherARM.UI.Views.Dialogs;

/// <summary>
/// Диалог подтверждения выполнения (доставки) заказа
/// </summary>
public partial class CompleteOrderDialog : Window
{
    private readonly Order _order;

    public CompleteOrderDialog(Order order)
    {
        InitializeComponent();
        _order = order;

        // Заполняем UI
        OrderInfoText.Text  = $"Заказ {order.OrderNumber}";
        ClientText.Text     = order.ClientName;
        AddressText.Text    = order.Address;
        PriceText.Text      = order.DeliveryPrice > 0 ? $"{order.DeliveryPrice:N0} ₽" : "Включена в стоимость";
        CodText.Text        = order.CashOnDelivery > 0 ? $"{order.CashOnDelivery:N0} ₽ (наложенный)" : "Нет";
        PaidCheck.IsChecked = order.CashOnDelivery == 0; // По умолчанию отмечаем оплаченным если нет наложки
    }

    private async void Confirm_Click(object sender, RoutedEventArgs e)
    {
        ConfirmBtn.IsEnabled = false;
        try
        {
            using var scope = App.Services.CreateScope();
            var orderRepo = scope.ServiceProvider.GetRequiredService<IOrderRepository>();

            var order = await orderRepo.GetByIdAsync(_order.Id);
            if (order == null) { ErrorText.Text = "Заказ не найден"; ErrorText.Visibility = Visibility.Visible; return; }

            order.Status      = OrderStatus.Delivered;
            order.CompletedAt = DateTime.Now;
            order.IsPaid      = PaidCheck.IsChecked == true;
            if (!string.IsNullOrWhiteSpace(NoteBox.Text))
                order.Notes = (order.Notes ?? "") + $"\n[{DateTime.Now:dd.MM HH:mm}] {NoteBox.Text}";

            orderRepo.Update(order);
            await orderRepo.SaveChangesAsync();

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            ErrorText.Text = $"Ошибка: {ex.Message}";
            ErrorText.Visibility = Visibility.Visible;
            ConfirmBtn.IsEnabled = true;
        }
    }
}
