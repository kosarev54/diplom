using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DispatcherARM.UI.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DispatcherARM.UI.Views.Pages;

public partial class IntegrationPage : UserControl
{
    private readonly OneCIntegrationService _svc;
    private readonly StringBuilder _log = new();
    private int _importedTotal;
    private int _exportedTotal;

    public IntegrationPage()
    {
        InitializeComponent();
        _svc = App.Services.GetRequiredService<OneCIntegrationService>();
        _svc.OrdersImported   += OnOrdersImported;
        _svc.StatusesExported += OnStatusesExported;
        _svc.ErrorOccurred    += OnError;

        // Восстановить путь из настроек
        var saved = Properties.Settings.Default.OneCExchangeFolder;
        if (!string.IsNullOrEmpty(saved)) FolderBox.Text = saved;

        UpdateWatchStatus();
    }

    private void Page_Loaded(object sender, RoutedEventArgs e) => UpdateWatchStatus();

    // ── Папка обмена ─────────────────────────────────────────────────────────

    private void BrowseFolder_Click(object sender, RoutedEventArgs e)
    {
        // OpenFolderDialog — доступен в WPF с .NET 8
        var dlg = new Microsoft.Win32.OpenFolderDialog
        {
            Title        = "Выберите папку для обмена с 1С",
            Multiselect  = false
        };
        if (!string.IsNullOrEmpty(FolderBox.Text))
            dlg.InitialDirectory = FolderBox.Text;

        if (dlg.ShowDialog() == true)
            FolderBox.Text = dlg.FolderName;
    }

    private void ApplyFolder_Click(object sender, RoutedEventArgs e)
    {
        var folder = FolderBox.Text.Trim();
        if (string.IsNullOrEmpty(folder)) { MessageBox.Show("Укажите путь к папке"); return; }

        try
        {
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            Properties.Settings.Default.OneCExchangeFolder = folder;
            Properties.Settings.Default.Save();
            AddLog($"[{DateTime.Now:HH:mm:ss}] ✅ Папка обмена установлена: {folder}");
            UpdateWatchStatus();
        }
        catch (Exception ex) { MessageBox.Show($"Ошибка: {ex.Message}"); }
    }

    // ── Мониторинг ────────────────────────────────────────────────────────────

    private void StartWatch_Click(object sender, RoutedEventArgs e)
    {
        var folder = FolderBox.Text.Trim();
        if (string.IsNullOrEmpty(folder))
        {
            MessageBox.Show("Сначала укажите папку обмена и нажмите «Применить»");
            return;
        }
        try
        {
            _svc.StartWatcher(folder);
            UpdateWatchStatus();
            AddLog($"[{DateTime.Now:HH:mm:ss}] ▶ Мониторинг запущен → {folder}");
        }
        catch (Exception ex) { MessageBox.Show($"Ошибка запуска: {ex.Message}"); }
    }

    private void StopWatch_Click(object sender, RoutedEventArgs e)
    {
        _svc.StopWatcher();
        UpdateWatchStatus();
        AddLog($"[{DateTime.Now:HH:mm:ss}] ⏹ Мониторинг остановлен");
    }

    private void UpdateWatchStatus()
    {
        Dispatcher.Invoke(() =>
        {
            bool watching = _svc.IsWatching;
            WatchDot.Fill         = new SolidColorBrush(watching ? Color.FromRgb(34, 197, 94) : Color.FromRgb(239, 68, 68));
            WatchText.Text        = watching ? "✅ Мониторинг активен — ожидаю файлы из 1С" : "⏹ Мониторинг не активен";
            WatchFolderText.Text  = $"Папка обмена: {(string.IsNullOrEmpty(FolderBox.Text) ? "не задана" : FolderBox.Text)}";
            StartWatchBtn.IsEnabled = !watching;
            StopWatchBtn.IsEnabled  =  watching;
        });
    }

    // ── Ручные операции ───────────────────────────────────────────────────────

    private async void ImportFile_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "JSON файлы (*.json)|*.json|Все файлы (*.*)|*.*",
            Title  = "Выберите файл экспорта из 1С"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            var (count, msg) = await _svc.ImportFromFileAsync(dlg.FileName);
            _importedTotal += count;
            ImportedCountText.Text = _importedTotal.ToString();
            LastSyncText.Text      = DateTime.Now.ToString("dd.MM HH:mm");
            AddLog($"[{DateTime.Now:HH:mm:ss}] 📥 {msg}");
            if (count > 0)
                MessageBox.Show($"✅ {msg}", "Импорт завершён", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) { AddLog($"[{DateTime.Now:HH:mm:ss}] ❌ Ошибка: {ex.Message}"); }
    }

    private async void ExportStatuses_Click(object sender, RoutedEventArgs e)
    {
        var folder = FolderBox.Text.Trim();
        if (string.IsNullOrEmpty(folder))
        {
            var dlg2 = new Microsoft.Win32.OpenFolderDialog
            {
                Title      = "Куда экспортировать статусы?",
                Multiselect = false
            };
            if (dlg2.ShowDialog() != true) return;
            folder = dlg2.FolderName;
        }
        try
        {
            var (count, msg) = await _svc.ExportStatusesAsync(folder);
            _exportedTotal += count;
            ExportedCountText.Text = _exportedTotal.ToString();
            LastSyncText.Text      = DateTime.Now.ToString("dd.MM HH:mm");
            AddLog($"[{DateTime.Now:HH:mm:ss}] 📤 {msg}");
            MessageBox.Show($"✅ {msg}", "Экспорт завершён", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) { AddLog($"[{DateTime.Now:HH:mm:ss}] ❌ Ошибка: {ex.Message}"); }
    }

    private async void GenerateSample_Click(object sender, RoutedEventArgs e)
    {
        var folder = FolderBox.Text.Trim();
        if (string.IsNullOrEmpty(folder))
            folder = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        try
        {
            var path = await OneCIntegrationService.GenerateSampleFileAsync(folder);
            AddLog($"[{DateTime.Now:HH:mm:ss}] 📋 Тестовый файл создан: {Path.GetFileName(path)}");
            MessageBox.Show($"✅ Файл создан:\n{path}\n\nИспользуйте кнопку «Импортировать файл из 1С» чтобы загрузить его.",
                "Тестовый файл", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) { MessageBox.Show($"Ошибка: {ex.Message}"); }
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        var folder = FolderBox.Text.Trim();
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
        { MessageBox.Show("Папка не задана или не существует"); return; }
        Process.Start("explorer.exe", folder);
    }

    // ── Лог ───────────────────────────────────────────────────────────────────

    private void ClearLog_Click(object sender, RoutedEventArgs e)
    {
        _log.Clear();
        LogText.Text = "Лог очищен.";
    }

    private void AddLog(string msg)
    {
        Dispatcher.Invoke(() =>
        {
            _log.AppendLine(msg);
            LogText.Text = _log.ToString();
            LogScroll.ScrollToBottom();
        });
    }

    // ── Обработчики событий сервиса ───────────────────────────────────────────

    private void OnOrdersImported(int count, string msg)
    {
        Dispatcher.Invoke(() =>
        {
            _importedTotal += count;
            ImportedCountText.Text = _importedTotal.ToString();
            LastSyncText.Text      = DateTime.Now.ToString("dd.MM HH:mm");
            AddLog($"[{DateTime.Now:HH:mm:ss}] 📥 (авто) {msg}");
        });
    }

    private void OnStatusesExported(int count, string msg)
    {
        Dispatcher.Invoke(() =>
        {
            _exportedTotal += count;
            ExportedCountText.Text = _exportedTotal.ToString();
            AddLog($"[{DateTime.Now:HH:mm:ss}] 📤 {msg}");
        });
    }

    private void OnError(string msg) => AddLog($"[{DateTime.Now:HH:mm:ss}] ❌ {msg}");
}
