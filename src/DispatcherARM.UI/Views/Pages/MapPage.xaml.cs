using System.Net;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using DispatcherARM.Domain.Entities;
using DispatcherARM.Domain.Enums;
using DispatcherARM.Domain.Interfaces;
using DispatcherARM.UI.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DispatcherARM.UI.Views.Pages;

public class RouteListItem
{
    public int    Id       { get; set; }
    public string Name     { get; set; } = "";
    public string Info     { get; set; } = "";
    public string ColorHex { get; set; } = "#2196F3";
}

public partial class MapPage : UserControl
{
    private List<Order>  _orders  = new();
    private List<Route>  _routes  = new();
    private int?         _selectedRouteId = null;
    private bool         _mapReady = false;

    private static readonly string[] Colors =
    {
        "#2196F3", "#4CAF50", "#FF9800", "#9C27B0", "#00BCD4", "#F44336"
    };

    public MapPage() { InitializeComponent(); }

    /// <summary>HTML-экранирование данных, попадающих в JS-строку карты</summary>
    private static string Enc(string? s) => WebUtility.HtmlEncode(s ?? string.Empty);

    private async void MapPage_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadDataAsync();
        BuildRoutesList();
        await InitMapAsync();
    }

    private async Task LoadDataAsync()
    {
        try
        {
            using var scope = App.Services.CreateScope();
            var orderRepo = scope.ServiceProvider.GetRequiredService<IOrderRepository>();
            var routeRepo = scope.ServiceProvider.GetRequiredService<IRouteRepository>();

            _orders = (await orderRepo.GetAllAsync()).ToList();

            var all = await routeRepo.GetAllAsync();
            var detailed = new List<Route>();
            foreach (var r in all)
            {
                var d = await routeRepo.GetRouteWithWaypointsAsync(r.Id);
                if (d != null) detailed.Add(d);
            }
            _routes = detailed;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"MapPage LoadData: {ex.Message}");
        }
    }

    private void BuildRoutesList()
    {
        var items = new List<RouteListItem>();
        int i = 0;
        foreach (var r in _routes.Where(r => r.Status != RouteStatus.Cancelled))
        {
            var statusText = r.Status switch
            {
                RouteStatus.Completed  => "✅ Завершён",
                RouteStatus.InProgress => "🚗 В пути",
                RouteStatus.Planned    => "📋 Запланирован",
                _                      => r.Status.ToString()
            };
            items.Add(new RouteListItem
            {
                Id       = r.Id,
                Name     = r.Name ?? $"Маршрут #{r.Id}",
                Info     = $"{statusText} · {r.TotalDistanceKm:F1} км",
                ColorHex = Colors[i % Colors.Length]
            });
            i++;
        }
        RoutesList.ItemsSource = items;
    }

    // ─── Инициализация WebView2 с Leaflet-картой ───────────────────────────────
    private async Task InitMapAsync()
    {
        try
        {
            await MapWebView.EnsureCoreWebView2Async(null);

            // ⚡ Ключевой фикс: задаём User-Agent как у обычного браузера Chrome
            // Без этого тайл-серверы блокируют запросы от WebView2
            MapWebView.CoreWebView2.Settings.UserAgent =
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
                "(KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

            MapWebView.CoreWebView2.Settings.IsStatusBarEnabled              = false;
            MapWebView.CoreWebView2.Settings.AreDevToolsEnabled              = false;
            MapWebView.CoreWebView2.Settings.IsZoomControlEnabled            = true;
            MapWebView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;

            var html = BuildMapHtml(null);
            MapWebView.NavigateToString(html);

            MapWebView.NavigationCompleted += (s, e) =>
            {
                Dispatcher.Invoke(() =>
                {
                    LoadingPanel.Visibility = Visibility.Collapsed;
                    MapWebView.Visibility   = Visibility.Visible;
                });
            };

            _mapReady = true;
        }
        catch (Exception ex)
        {
            LoadingPanel.Visibility = Visibility.Collapsed;
            System.Diagnostics.Debug.WriteLine($"MapPage WebView2 init: {ex.Message}");
            Dispatcher.Invoke(() =>
            {
                var tb = new TextBlock
                {
                    Text = $"Карта недоступна: {ex.Message}\n\nТребуется Microsoft Edge WebView2 Runtime.",
                    TextWrapping = TextWrapping.Wrap, Margin = new Thickness(24),
                    Foreground = System.Windows.Media.Brushes.Gray, FontSize = 13
                };
                var mapGrid = MapWebView.Parent as Grid;
                mapGrid?.Children.Add(tb);
                LoadingPanel.Visibility = Visibility.Collapsed;
            });
        }
    }

    // ─── Генерация HTML с Leaflet.js ───────────────────────────────────────────
    private string BuildMapHtml(int? highlightRouteId)
    {
        var sb = new StringBuilder();

        sb.AppendLine("<!DOCTYPE html><html><head>");
        sb.AppendLine("<meta charset='utf-8'/>");
        sb.AppendLine("<meta name='viewport' content='width=device-width,initial-scale=1'/>");
        sb.AppendLine("<link rel='stylesheet' href='https://unpkg.com/leaflet@1.9.4/dist/leaflet.css'/>");
        sb.AppendLine("<script src='https://unpkg.com/leaflet@1.9.4/dist/leaflet.js'></script>");
        sb.AppendLine("<style>*{margin:0;padding:0;box-sizing:border-box}html,body,#map{height:100%;width:100%}</style>");
        sb.AppendLine("</head><body>");
        sb.AppendLine("<div id='map'></div><script>");
        var depotLat = AppSettings.Instance.DepotLat.ToString("F6", System.Globalization.CultureInfo.InvariantCulture);
        var depotLon = AppSettings.Instance.DepotLon.ToString("F6", System.Globalization.CultureInfo.InvariantCulture);
        var depotAddr = System.Net.WebUtility.HtmlEncode(AppSettings.Instance.DepotAddress);

        sb.AppendLine($"var map = L.map('map',{{zoomControl:true}}).setView([{depotLat},{depotLon}],11);");
        // Esri World Street Map — бесплатно, не блокирует запросы, стабильный сервис
        sb.AppendLine("L.tileLayer('https://server.arcgisonline.com/ArcGIS/rest/services/World_Street_Map/MapServer/tile/{z}/{y}/{x}',{attribution:'Tiles &copy; Esri &mdash; Source: Esri, DeLorme, NAVTEQ',maxZoom:19}).addTo(map);");

        // Depot
        sb.AppendLine("var depotIcon=L.divIcon({html:'<div style=\"background:#1E3A5F;color:white;border-radius:50%;width:30px;height:30px;display:flex;align-items:center;justify-content:center;font-size:16px;border:2px solid white;box-shadow:0 2px 6px rgba(0,0,0,0.3)\">🏢</div>',className:'',iconSize:[30,30],iconAnchor:[15,15]});");
        sb.AppendLine($"L.marker([{depotLat},{depotLon}],{{icon:depotIcon}}).addTo(map).bindPopup('<b>Депо</b><br>{depotAddr}');");

        bool showOrders = ShowOrdersCheck?.IsChecked ?? true;
        bool showRoutes = ShowRoutesCheck?.IsChecked ?? true;

        // Заказы
        if (showOrders)
        {
            int ord = 0;
            foreach (var order in _orders.Where(o => o.Latitude != 0 || o.Longitude != 0))
            {
                var color = order.Status switch
                {
                    OrderStatus.New       => "#2196F3",
                    OrderStatus.Assigned  => "#FF9800",
                    OrderStatus.InTransit => "#9C27B0",
                    OrderStatus.Delivered => "#4CAF50",
                    OrderStatus.Cancelled => "#9E9E9E",
                    _                     => "#F44336"
                };
                var statusRu = order.Status switch
                {
                    OrderStatus.New       => "Новый",
                    OrderStatus.Assigned  => "Назначен",
                    OrderStatus.InTransit => "В пути",
                    OrderStatus.Delivered => "Доставлен",
                    OrderStatus.Cancelled => "Отменён",
                    _                     => order.Status.ToString()
                };
                // Экранируем и HTML, и JS-строку: данные приходят из веб-формы и 1С
                var popup = $"<b>{Enc(order.OrderNumber)}</b><br>{Enc(order.ClientName)}<br>{Enc(order.Address)}" +
                            $"<br>Статус: {statusRu}<br>Вес: {order.Weight} кг<br>Цена: {order.DeliveryPrice:F0} ₽";
                popup = popup.Replace("\\", "\\\\").Replace("'", "\\'");
                sb.AppendLine($"L.circleMarker([{order.Latitude.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)},{order.Longitude.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)}],{{radius:8,fillColor:'{color}',color:'white',weight:2,fillOpacity:0.9}}).addTo(map).bindPopup('{popup}');");
                ord++;
            }
        }

        // Маршруты
        if (showRoutes)
        {
            int ci = 0;
            foreach (var route in _routes.Where(r => r.Status != RouteStatus.Cancelled))
            {
                var color    = Colors[ci % Colors.Length];
                ci++;
                bool isHL    = highlightRouteId.HasValue && route.Id == highlightRouteId.Value;
                bool isDimmed = highlightRouteId.HasValue && !isHL;
                double opacity = isDimmed ? 0.2 : 0.9;
                int    weight  = isHL ? 6 : 3;

                var wps = route.Waypoints?.OrderBy(w => w.SequenceNumber).ToList();
                if (wps == null || wps.Count < 1) continue;

                // Координаты: Депо → точки маршрута (маршрут открытый — нет возврата в депо)
                var coords = new List<string> { $"[{depotLat},{depotLon}]" };
                foreach (var wp in wps)
                {
                    coords.Add($"[{wp.Latitude.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)},{wp.Longitude.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)}]");
                }
                // НЕ добавляем возврат в депо (открытый маршрут — доставка, а не кольцевой рейс)

                var dashStyle = route.Status == RouteStatus.Completed ? ",dashArray:'8,8'" : "";
                var routeName = (route.Name ?? $"Маршрут #{route.Id}").Replace("'", "\\'");
                sb.AppendLine($"L.polyline([{string.Join(",", coords)}],{{color:'{color}',weight:{weight},opacity:{opacity.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)}{dashStyle}}}).addTo(map).bindPopup('{routeName}');");

                // Маркеры точек маршрута
                int seq = 1;
                foreach (var wp in wps)
                {
                    var addr = (wp.Order?.Address ?? $"Точка {seq}").Replace("'", "\\'");
                    sb.AppendLine($"L.circleMarker([{wp.Latitude.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)},{wp.Longitude.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)}],{{radius:{(isHL?7:4)},fillColor:'{color}',color:'white',weight:2,fillOpacity:{opacity.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)}}}).addTo(map).bindPopup('<b>Точка {seq}</b><br>{addr}');");
                    seq++;
                }
            }
        }

        sb.AppendLine("</script></body></html>");
        return sb.ToString();
    }

    // ─── Выбор маршрута ─────────────────────────────────────────────────────────
    private async void RoutesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (RoutesList.SelectedItem is not RouteListItem item) return;
        _selectedRouteId = item.Id;
        SelectedRouteText.Text = $"Выделен: {item.Name}";
        SelectedRouteBanner.Visibility = Visibility.Visible;
        await RefreshMap(item.Id);
    }

    private async void ClearSelection_Click(object sender, RoutedEventArgs e) => await ShowAllRoutes();
    private async void ShowAll_Click(object sender, RoutedEventArgs e) => await ShowAllRoutes();

    private async Task ShowAllRoutes()
    {
        _selectedRouteId = null;
        RoutesList.SelectedItem = null;
        SelectedRouteBanner.Visibility = Visibility.Collapsed;
        await RefreshMap(null);
    }

    private async Task RefreshMap(int? highlightId)
    {
        if (!_mapReady) return;
        var html = BuildMapHtml(highlightId);
        MapWebView.NavigateToString(html);
        await Task.CompletedTask;
    }

    private void LayerChanged(object sender, RoutedEventArgs e)
    {
        if (!_mapReady) return;
        _ = RefreshMap(_selectedRouteId);
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await LoadDataAsync();
        BuildRoutesList();
        await RefreshMap(_selectedRouteId);
    }

    public async Task RefreshAfterRouteBuilt()
    {
        await LoadDataAsync();
        BuildRoutesList();
        await RefreshMap(null);
    }
}
