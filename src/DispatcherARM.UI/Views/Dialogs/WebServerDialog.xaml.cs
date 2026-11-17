using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using DispatcherARM.UI.Services;

namespace DispatcherARM.UI.Views.Dialogs;

public partial class WebServerDialog : Window
{
    private readonly WebOrderServer _server;
    private readonly System.Text.StringBuilder _log = new();

    public WebServerDialog(WebOrderServer server)
    {
        InitializeComponent();
        _server = server;
        _server.OrderReceived += OnOrderReceived;
        UpdateStatus();
    }

    private void StartServer_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _server.Start();
            UpdateStatus();
            AddLog($"[{DateTime.Now:HH:mm:ss}] ✅ Сервер запущен — {WebOrderServer.Url}");
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Не удалось запустить сервер:\n{ex.Message}\n\n" +
                "Убедитесь что порт 5050 не занят другим приложением.",
                "Ошибка запуска", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void StopServer_Click(object sender, RoutedEventArgs e)
    {
        _server.Stop();
        UpdateStatus();
        AddLog($"[{DateTime.Now:HH:mm:ss}] ⏹ Сервер остановлен");
    }

    private void OpenBrowser_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(WebOrderServer.Url) { UseShellExecute = true });
        }
        catch
        {
            MessageBox.Show("Перейдите в браузере по адресу:\n" + WebOrderServer.Url);
        }
    }

    private void UpdateStatus()
    {
        bool running = _server.IsRunning;

        StatusDot.Fill = new SolidColorBrush(running
            ? Color.FromRgb(34, 197, 94)
            : Color.FromRgb(239, 68, 68));

        StatusBorder.Background = new SolidColorBrush(running
            ? Color.FromRgb(240, 253, 244)
            : Color.FromRgb(254, 242, 242));

        StatusText.Text = running ? "✅ Сервер работает" : "⏹ Сервер остановлен";
        StatusUrl.Text  = running
            ? $"Клиентская форма: {WebOrderServer.Url}"
            : "Нажмите «Запустить» чтобы начать принимать заявки";

        StartBtn.IsEnabled = !running;
        StopBtn.IsEnabled  =  running;
    }

    private void OnOrderReceived(string orderNumber)
    {
        // Вызывается из фонового потока → нужен Dispatcher
        Dispatcher.Invoke(() =>
        {
            AddLog($"[{DateTime.Now:HH:mm:ss}] 📦 НОВАЯ ЗАЯВКА: {orderNumber}");
        });
    }

    private void AddLog(string msg)
    {
        _log.AppendLine(msg);
        LogText.Text = _log.ToString();
        LogScroll.ScrollToBottom();
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        _server.OrderReceived -= OnOrderReceived;
        // Сервер НЕ останавливаем при закрытии окна — продолжает работать
    }
}
