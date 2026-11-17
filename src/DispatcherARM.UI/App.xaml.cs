using System.IO;
using System.Windows;
using DispatcherARM.Application.Services;
using DispatcherARM.Domain.Interfaces;
using DispatcherARM.Infrastructure.Data;
using DispatcherARM.Infrastructure.Repositories;
using DispatcherARM.Infrastructure.Routing;
using DispatcherARM.UI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DispatcherARM.UI;

public partial class App : System.Windows.Application
{
    public static IServiceProvider Services { get; private set; } = null!;
    public static bool UsePostgres { get; set; } = false;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // ─── Глобальный перехват ошибок — НИКОГДА не закрываем приложение молча ───
        DispatcherUnhandledException += (s, ex) =>
        {
            var msg = ex.Exception?.Message ?? "Неизвестная ошибка";
            var inner = ex.Exception?.InnerException?.Message;
            var full = inner != null ? $"{msg}\n\nДетали: {inner}" : msg;

            AppLogger.Error("Необработанное исключение (Dispatcher)", ex.Exception);
            MessageBox.Show(
                $"Произошла ошибка:\n\n{full}\n\nПриложение продолжит работу.",
                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            ex.Handled = true; // НЕ закрываем приложение
        };

        AppDomain.CurrentDomain.UnhandledException += (s, ex) =>
        {
            var msg = (ex.ExceptionObject as Exception)?.Message ?? ex.ExceptionObject?.ToString();
            MessageBox.Show($"Критическая ошибка:\n{msg}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        };

        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();

        try
        {
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await context.Database.EnsureCreatedAsync();
            await SeedData.SeedAsync(context);
            AppLogger.Info("Приложение запущено. БД инициализирована успешно.");
        }
        catch (Exception ex)
        {
            AppLogger.Error("Ошибка инициализации БД", ex);
            MessageBox.Show(
                $"Ошибка подключения к БД:\n{ex.Message}\n\nПриложение будет запущено, но данные могут быть недоступны.",
                "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        if (UsePostgres)
        {
            services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql("Host=localhost;Port=5432;Database=dispatcher_arm;Username=dispatcher;Password=dispatcher_pass"));
        }
        else
        {
            // ── P0-10 FIX: БД в %LocalAppData%\DispatcherARM\ — не теряется при пересборке ──
            var appDataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DispatcherARM");
            Directory.CreateDirectory(appDataDir);
            var dbPath = Path.Combine(appDataDir, "dispatcher_arm.db");
            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlite($"Data Source={dbPath}"));
        }

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IDriverRepository, DriverRepository>();
        services.AddScoped<IVehicleRepository, VehicleRepository>();
        services.AddScoped<IRouteRepository, RouteRepository>();
        services.AddScoped<ITransactionRepository, TransactionRepository>();

        var osrmSettings = new OsrmSettings();
        services.AddSingleton(osrmSettings);
        services.AddHttpClient<OsrmClient>();

        services.AddScoped<IRouteOptimizer, NearestNeighborOptimizer>();
        services.AddScoped<IRouteOptimizer, TwoOptOptimizer>();
        services.AddScoped<IRouteOptimizer, OsrmTripOptimizer>();

        services.AddScoped<AuthService>();
        // ── RouteService: координаты депо из настроек, не из кода ────────────────
        services.AddScoped<RouteService>(sp =>
        {
            var svc = ActivatorUtilities.CreateInstance<RouteService>(sp);
            var s = AppSettings.Instance;
            svc.DepotLocation = (s.DepotLat, s.DepotLon);
            return svc;
        });

        // Веб-сервер — синглтон, получает root IServiceProvider
        services.AddSingleton<WebOrderServer>(sp => new WebOrderServer(sp));

        // Сервис интеграции с 1С — синглтон
        services.AddSingleton<OneCIntegrationService>(sp => new OneCIntegrationService(sp));
    }
}
