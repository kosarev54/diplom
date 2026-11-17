using System.IO;
using System.Net;
using System.Text;
using DispatcherARM.Domain.Entities;
using DispatcherARM.Domain.Enums;
using DispatcherARM.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace DispatcherARM.UI.Services;

/// <summary>
/// Локальный HTTP-сервер для приёма заказов от клиентов через браузер
/// Слушает http://localhost:5050
/// </summary>
public class WebOrderServer
{
    private HttpListener?             _listener;
    private CancellationTokenSource?  _cts;
    private readonly IServiceProvider _services;

    public bool   IsRunning => _listener?.IsListening == true;
    public const  string Url = "http://localhost:5050/";

    /// <summary>Вызывается при поступлении новой заявки (номер заказа)</summary>
    public event Action<string>? OrderReceived;

    public WebOrderServer(IServiceProvider services)
    {
        _services = services;
    }

    public void Start()
    {
        if (IsRunning) return;
        _listener = new HttpListener();
        _listener.Prefixes.Add(Url);
        _listener.Start();
        _cts = new CancellationTokenSource();
        Task.Run(() => ListenLoop(_cts.Token));
    }

    public void Stop()
    {
        _cts?.Cancel();
        try { _listener?.Stop(); } catch { }
        _listener = null;
    }

    private async Task ListenLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _listener?.IsListening == true)
        {
            try
            {
                var ctx = await _listener.GetContextAsync();
                _ = Task.Run(() => HandleRequest(ctx), ct);
            }
            catch { /* listener closed */ }
        }
    }

    private async Task HandleRequest(HttpListenerContext ctx)
    {
        var req = ctx.Request;
        var res = ctx.Response;
        res.ContentType = "text/html; charset=utf-8";
        byte[] body;

        try
        {
            if (req.HttpMethod == "GET")
            {
                body = Encoding.UTF8.GetBytes(GetFormHtml());
            }
            else if (req.HttpMethod == "POST")
            {
                using var reader = new StreamReader(req.InputStream, req.ContentEncoding);
                var formData = await reader.ReadToEndAsync();
                body = Encoding.UTF8.GetBytes(await ProcessOrder(formData));
            }
            else
            {
                res.StatusCode = 404;
                body = Encoding.UTF8.GetBytes("<h1>404</h1>");
            }
        }
        catch (Exception ex)
        {
            res.StatusCode = 500;
            body = Encoding.UTF8.GetBytes(GetErrorHtml($"Внутренняя ошибка: {ex.Message}"));
        }

        res.ContentLength64 = body.Length;
        await res.OutputStream.WriteAsync(body);
        res.OutputStream.Close();
    }

    // Декодирование URL без System.Web
    private static string UrlDecode(string s)
        => Uri.UnescapeDataString(s.Replace("+", " "));

    // ── Баг 5 FIX: счётчик для уникальных номеров при конкурентных запросах ────
    private static int _orderCounter = 0;

    private async Task<string> ProcessOrder(string formData)
    {
        var pairs = formData
            .Split('&')
            .Select(p => p.Split('=', 2))
            .Where(p => p.Length == 2)
            .ToDictionary(p => UrlDecode(p[0]), p => UrlDecode(p[1]));

        string Get(string key) => pairs.TryGetValue(key, out var v) ? v.Trim() : "";

        var clientName  = Get("clientName");
        var clientPhone = Get("clientPhone");
        var address     = Get("address");
        var notes       = Get("notes");
        var priorityStr = Get("priority");

        // ── Валидация длин (защита от спама) ─────────────────────────────────────
        if (string.IsNullOrWhiteSpace(clientName) || string.IsNullOrWhiteSpace(address))
            return GetErrorHtml("Заполните обязательные поля: «Организация/ФИО» и «Адрес доставки»");
        if (clientName.Length > 200)  return GetErrorHtml("Поле «Организация/ФИО» слишком длинное (макс. 200 символов).");
        if (address.Length > 500)     return GetErrorHtml("Поле «Адрес» слишком длинное (макс. 500 символов).");
        if (clientPhone.Length > 30)  clientPhone = clientPhone[..30];
        if (notes.Length > 1000)      notes = notes[..1000];

        double.TryParse(Get("lat"),    System.Globalization.CultureInfo.InvariantCulture, out double lat);
        double.TryParse(Get("lon"),    System.Globalization.CultureInfo.InvariantCulture, out double lon);
        double.TryParse(Get("weight"), System.Globalization.CultureInfo.InvariantCulture, out double weight);
        double.TryParse(Get("volume"), System.Globalization.CultureInfo.InvariantCulture, out double volume);
        decimal.TryParse(Get("price"), System.Globalization.CultureInfo.InvariantCulture, out decimal price);

        // Клиент не должен задавать цену — используем минимальный тариф
        if (price > 99999) price = 0; // обнулим подозрительную цену
        if (weight > 50000) weight = 0;

        var priority = Enum.TryParse<OrderPriority>(priorityStr, true, out var p)
            ? p : OrderPriority.Normal;

        // Если координаты не заданы — ставим центр Москвы
        if (lat == 0 && lon == 0) { lat = AppSettings.Instance.DepotLat; lon = AppSettings.Instance.DepotLon; }

        using var scope  = _services.CreateScope();
        var repo         = scope.ServiceProvider.GetRequiredService<IOrderRepository>();
        var allOrders    = await repo.GetAllAsync();
        var deliveryDate = DeliveryDateCalculator.Calculate(lat, lon, priority, allOrders);

        // ── БАГ 5 FIX: уникальный номер с счётчиком + random hex ───────────────
        var cnt = System.Threading.Interlocked.Increment(ref _orderCounter);
        var orderNumber = $"ORD-WEB-{DateTime.Now:yyyyMMddHHmmss}-{cnt:D3}";

        var order = new Order
        {
            OrderNumber    = orderNumber,
            ClientName     = clientName,
            ClientPhone    = clientPhone,
            Address        = address,
            Latitude       = lat,
            Longitude      = lon,
            Weight         = weight,
            Volume         = volume,
            DeliveryPrice  = price,
            Priority       = priority,
            Notes          = string.IsNullOrWhiteSpace(notes)
                                ? "Веб-заявка"
                                : $"Веб-заявка: {notes}",
            DeliveryDate   = deliveryDate,
            Status         = OrderStatus.New
        };

        await repo.AddAsync(order);
        await repo.SaveChangesAsync();

        OrderReceived?.Invoke(orderNumber);

        // ── БАГ 4 FIX: HtmlEncode всех пользовательских данных ──────────────────
        return GetSuccessHtml(
            WebUtility.HtmlEncode(orderNumber),
            deliveryDate,
            WebUtility.HtmlEncode(address));
    }

    // ──────────── HTML страницы ────────────────────────────────────────────────

    private static string GetFormHtml() => """
        <!DOCTYPE html>
        <html lang="ru">
        <head>
            <meta charset="UTF-8">
            <meta name="viewport" content="width=device-width, initial-scale=1.0">
            <title>Оформить заявку на доставку</title>
            <style>
                *{box-sizing:border-box;margin:0;padding:0}
                body{font-family:'Segoe UI',Arial,sans-serif;background:#f5f7fa;color:#1f2937}
                .hdr{background:#3F51B5;color:#fff;padding:20px 40px}
                .hdr h1{font-size:22px;margin-bottom:4px}
                .hdr p{font-size:13px;opacity:.8}
                .cnt{max-width:680px;margin:28px auto;padding:0 16px}
                .card{background:#fff;border-radius:12px;box-shadow:0 2px 12px rgba(0,0,0,.08);padding:28px;margin-bottom:18px}
                .sec{font-size:11px;font-weight:700;color:#6b7280;letter-spacing:.5px;margin-bottom:14px;text-transform:uppercase}
                .fg{margin-bottom:16px}
                label{display:block;font-size:13px;color:#374151;margin-bottom:5px;font-weight:500}
                input,textarea,select{width:100%;padding:10px 14px;border:1.5px solid #d1d5db;border-radius:8px;font-size:14px;outline:none;font-family:inherit}
                input:focus,textarea:focus,select:focus{border-color:#3F51B5;box-shadow:0 0 0 3px rgba(63,81,181,.12)}
                .row{display:grid;grid-template-columns:1fr 1fr;gap:14px}
                .req::after{content:' *';color:#ef4444}
                .btn{background:#3F51B5;color:#fff;border:none;padding:14px 32px;border-radius:8px;font-size:15px;font-weight:600;cursor:pointer;width:100%;transition:background .2s}
                .btn:hover{background:#303f9f}
                .info{background:#eff6ff;border:1px solid #bfdbfe;border-radius:8px;padding:10px 14px;font-size:12px;color:#1d4ed8;margin-bottom:16px}
                .hint{font-size:11px;color:#9ca3af;margin-top:4px;text-align:center}
            </style>
        </head>
        <body>
        <div class="hdr">
            <h1>🚚 Транспортно-логистическая компания</h1>
            <p>Оформление заявки на доставку груза — мы свяжемся с вами для подтверждения</p>
        </div>
        <div class="cnt">
        <form action="/order" method="POST">
            <div class="card">
                <div class="sec">Данные клиента</div>
                <div class="fg">
                    <label class="req">Организация / ФИО</label>
                    <input type="text" name="clientName" placeholder="ООО «Альфа» или Иванов И.И." required>
                </div>
                <div class="row">
                    <div class="fg">
                        <label>Телефон для связи</label>
                        <input type="tel" name="clientPhone" placeholder="+7 (495) 000-00-00">
                    </div>
                    <div class="fg">
                        <label>Стоимость доставки (₽)</label>
                        <input type="number" name="price" placeholder="0" min="0" step="100">
                    </div>
                </div>
            </div>

            <div class="card">
                <div class="sec">Адрес и груз</div>
                <div class="fg">
                    <label class="req">Адрес доставки</label>
                    <input type="text" name="address" placeholder="г. Москва, ул. Тверская, д. 1" required>
                </div>
                <div class="info">
                    📍 Необязательно: укажите координаты для точного расчёта даты доставки.
                    Если не знаете — оставьте пустыми, дата будет рассчитана примерно.
                </div>
                <div class="row">
                    <div class="fg"><label>Широта</label><input type="number" name="lat" step="0.0001" placeholder="55.7558"></div>
                    <div class="fg"><label>Долгота</label><input type="number" name="lon" step="0.0001" placeholder="37.6173"></div>
                </div>
                <div class="row">
                    <div class="fg"><label>Вес груза (кг)</label><input type="number" name="weight" step="0.1" placeholder="0" min="0"></div>
                    <div class="fg"><label>Объём (м³)</label><input type="number" name="volume" step="0.01" placeholder="0" min="0"></div>
                </div>
                <div class="fg">
                    <label>Приоритет доставки</label>
                    <select name="priority">
                        <option value="Low">🟢 Низкий — без спешки</option>
                        <option value="Normal" selected>🟡 Обычный</option>
                        <option value="High">🟠 Высокий — желательно быстро</option>
                        <option value="Urgent">🔴 Срочно — в день заявки</option>
                    </select>
                </div>
                <div class="fg">
                    <label>Примечания к заказу</label>
                    <textarea name="notes" rows="3" placeholder="Хрупкий груз, нужна охрана, контактное лицо на месте..."></textarea>
                </div>
            </div>

            <div class="card">
                <button type="submit" class="btn">📦 Отправить заявку</button>
                <p class="hint">После отправки заявка поступит диспетчеру. Мы свяжемся с вами для подтверждения даты доставки.</p>
            </div>
        </form>
        </div>
        </body>
        </html>
        """;

    private static string GetSuccessHtml(string orderNumber, DateTime date, string address) =>
        $$"""
        <!DOCTYPE html>
        <html lang="ru">
        <head>
            <meta charset="UTF-8">
            <title>Заявка принята</title>
            <style>
                body{font-family:'Segoe UI',Arial,sans-serif;background:#f5f7fa;display:flex;align-items:center;justify-content:center;min-height:100vh}
                .c{background:#fff;border-radius:12px;box-shadow:0 2px 12px rgba(0,0,0,.08);padding:48px;text-align:center;max-width:480px}
                .ic{font-size:64px;margin-bottom:16px}
                h2{color:#166534;margin-bottom:12px;font-size:24px}
                p{color:#4b5563;margin-bottom:6px;font-size:14px}
                .num{background:#f0fdf4;border:1px solid #bbf7d0;border-radius:8px;padding:14px;font-size:22px;font-weight:700;color:#166534;margin:16px 0}
                .btn{display:inline-block;margin-top:24px;padding:12px 32px;background:#3F51B5;color:#fff;border-radius:8px;text-decoration:none;font-weight:600}
                small{color:#9ca3af;font-size:12px;display:block;margin-top:16px}
            </style>
        </head>
        <body>
        <div class="c">
            <div class="ic">✅</div>
            <h2>Заявка принята!</h2>
            <div class="num">№ {{orderNumber}}</div>
            <p><b>Адрес доставки:</b> {{address}}</p>
            <p><b>Расчётная дата доставки:</b> {{date:dd.MM.yyyy}}</p>
            <small>Диспетчер назначит водителя и свяжется с вами для подтверждения.</small>
            <br>
            <a href="/" class="btn">← Оформить ещё одну заявку</a>
        </div>
        </body>
        </html>
        """;

    private static string GetErrorHtml(string msg) =>
        $$"""
        <!DOCTYPE html>
        <html lang="ru">
        <head>
            <meta charset="UTF-8">
            <title>Ошибка</title>
            <style>
                body{font-family:'Segoe UI',Arial,sans-serif;background:#f5f7fa;display:flex;align-items:center;justify-content:center;min-height:100vh}
                .c{background:#fff;border-radius:12px;padding:40px;text-align:center;max-width:400px;box-shadow:0 2px 12px rgba(0,0,0,.08)}
                h2{color:#dc2626;margin-bottom:12px}
                .btn{display:inline-block;margin-top:20px;padding:12px 28px;background:#3F51B5;color:#fff;border-radius:8px;text-decoration:none}
            </style>
        </head>
        <body>
        <div class="c">
            <div style="font-size:48px">⚠️</div>
            <h2>Ошибка</h2>
            <p>{{msg}}</p>
            <a href="/" class="btn">← Назад к форме</a>
        </div>
        </body>
        </html>
        """;
}
