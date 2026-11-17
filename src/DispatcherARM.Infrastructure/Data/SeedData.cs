using DispatcherARM.Domain.Entities;
using DispatcherARM.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DispatcherARM.Infrastructure.Data;

/// <summary>
/// Начальные данные для БД
/// </summary>
public static class SeedData
{
    public static async Task SeedAsync(AppDbContext context)
    {
        await context.Database.EnsureCreatedAsync();

        if (await context.Users.AnyAsync())
            return;

        // ──────────── ПОЛЬЗОВАТЕЛИ ────────────
        var adminUser      = new User { Login = "admin",      PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),  Role = UserRole.Admin,      FullName = "Администратор Системы" };
        var dispatcherUser = new User { Login = "dispatcher", PasswordHash = BCrypt.Net.BCrypt.HashPassword("disp123"),   Role = UserRole.Dispatcher, FullName = "Иванов Иван Иванович" };
        var driverUser1    = new User { Login = "driver1",    PasswordHash = BCrypt.Net.BCrypt.HashPassword("driver123"), Role = UserRole.Driver,     FullName = "Петров Пётр Петрович" };
        var driverUser2    = new User { Login = "driver2",    PasswordHash = BCrypt.Net.BCrypt.HashPassword("driver123"), Role = UserRole.Driver,     FullName = "Сидоров Сидор Сидорович" };
        var driverUser3    = new User { Login = "driver3",    PasswordHash = BCrypt.Net.BCrypt.HashPassword("driver123"), Role = UserRole.Driver,     FullName = "Козлов Андрей Викторович" };

        await context.Users.AddRangeAsync(adminUser, dispatcherUser, driverUser1, driverUser2, driverUser3);
        await context.SaveChangesAsync();

        // ──────────── ТРАНСПОРТНЫЕ СРЕДСТВА ────────────
        var today = DateTime.Today;
        var vehicle1 = new Vehicle
        {
            LicensePlate = "А001АА77", Model = "ГАЗель NEXT", Year = 2021, Vin = "XUF3302420K000001",
            Type = VehicleType.Van,   MaxWeightKg = 1500, MaxVolumeM3 = 9.0,
            FuelConsumptionPer100Km = 12.5, OdometerKm = 48320,
            LastMaintenanceDate = today.AddDays(-45), LastMaintenanceOdometerKm = 45000,
            NextMaintenanceDate = today.AddDays(45),  NextMaintenanceKm = 10000,
            InsurancePolicy = "ПАО-12345678", InsuranceExpiry = today.AddMonths(8),
            Status = VehicleStatus.Available, Notes = "Фургон-будка, боковая дверь"
        };
        var vehicle2 = new Vehicle
        {
            LicensePlate = "В002ВВ77", Model = "КАМАЗ 4308", Year = 2019, Vin = "XTC432610K0000002",
            Type = VehicleType.Truck,  MaxWeightKg = 5000, MaxVolumeM3 = 25.0,
            FuelConsumptionPer100Km = 22.0, OdometerKm = 127400,
            LastMaintenanceDate = today.AddDays(-10), LastMaintenanceOdometerKm = 127000,
            NextMaintenanceDate = today.AddDays(80),  NextMaintenanceKm = 10000,
            InsurancePolicy = "ПАО-87654321", InsuranceExpiry = today.AddMonths(3),
            MonthlyCost = 65000, Status = VehicleStatus.Available
        };
        var vehicle3 = new Vehicle
        {
            LicensePlate = "С003СС77", Model = "Ford Transit", Year = 2022, Vin = "WF0WXXTTGWKC00003",
            Type = VehicleType.Van,   MaxWeightKg = 1200, MaxVolumeM3 = 7.5,
            FuelConsumptionPer100Km = 10.0, OdometerKm = 32100,
            LastMaintenanceDate = today.AddDays(-90), LastMaintenanceOdometerKm = 28000,
            NextMaintenanceDate = today.AddDays(10),  NextMaintenanceKm = 10000,
            InsurancePolicy = "ПАО-11223344", InsuranceExpiry = today.AddMonths(11),
            MonthlyCost = 55000, Status = VehicleStatus.Available,
            Notes = "Климат-контроль в кузове"
        };

        await context.Vehicles.AddRangeAsync(vehicle1, vehicle2, vehicle3);
        await context.SaveChangesAsync();

        // ──────────── ВОДИТЕЛИ (разные графики, не совпадают выходные) ────────────
        // driver1: 5/2 (пн-пт рабочие, сб-вс выходные)
        // driver2: 2/2 (чётные дни недели — рабочие, нечётные — выходные)
        // driver3: 6/1 (пн-сб рабочие, вс выходной)
        var driver1 = new Driver
        {
            FullName = "Петров Пётр Петрович",       Phone = "+7 (999) 111-11-11",
            LicenseNumber = "77 01 123456",           LicenseCategory = "B, C",
            LicenseExpiryDate = today.AddYears(3),    BirthDate = new DateTime(1985, 3, 15),
            Email = "petrov@logistic.ru",             Address = "г. Москва, ул. Садовая, д. 12, кв. 34",
            HireDate = new DateTime(2020, 6, 1),      WorkSchedule = "5/2 (пн–пт)",
            BaseSalary = 55000, RatePerKm = 2, RatePerDelivery = 200,
            Status = DriverStatus.Available, AssignedVehicleId = vehicle1.Id, UserId = driverUser1.Id,
            Notes = "Опыт 8 лет, знает Москву и МО"
        };
        var driver2 = new Driver
        {
            FullName = "Сидоров Сидор Сидорович",    Phone = "+7 (999) 222-22-22",
            LicenseNumber = "77 02 654321",           LicenseCategory = "C, E",
            LicenseExpiryDate = today.AddYears(2),    BirthDate = new DateTime(1979, 7, 22),
            Email = "sidorov@logistic.ru",            Address = "г. Москва, пр-т Мира, д. 45, кв. 67",
            HireDate = new DateTime(2018, 1, 15),     WorkSchedule = "2/2 (чт–пт, пн–вт)",
            BaseSalary = 65000, RatePerKm = 3, RatePerDelivery = 250,
            Status = DriverStatus.Available, AssignedVehicleId = vehicle2.Id, UserId = driverUser2.Id,
            Notes = "Категория E, тяжёлые грузы, межгород"
        };
        var driver3 = new Driver
        {
            FullName = "Козлов Андрей Викторович",   Phone = "+7 (999) 333-33-33",
            LicenseNumber = "77 03 789012",           LicenseCategory = "B, C",
            LicenseExpiryDate = today.AddYears(4),    BirthDate = new DateTime(1992, 11, 5),
            Email = "kozlov@logistic.ru",             Address = "г. Москва, Ленинградское ш., д. 8, кв. 12",
            HireDate = new DateTime(2022, 3, 1),      WorkSchedule = "6/1 (пн–сб)",
            BaseSalary = 50000, RatePerKm = 2, RatePerDelivery = 180,
            Status = DriverStatus.Available, AssignedVehicleId = vehicle3.Id, UserId = driverUser3.Id,
            Notes = "Молодой, аккуратный, пунктуальный"
        };

        await context.Drivers.AddRangeAsync(driver1, driver2, driver3);
        await context.SaveChangesAsync();

        // ──────────── ЗАКАЗЫ ────────────
        // Часть уже выполнена (Delivered/InTransit), часть новые
        var orders = new List<Order>
        {
            // === ВЫПОЛНЕННЫЕ (прошлые дни) ===
            new() {
                OrderNumber = "ORD-0001", ClientName = "ООО «Альфа»", ClientPhone = "+7 (495) 100-10-01",
                Address = "г. Москва, ул. Тверская, д. 1",
                Latitude = 55.7558, Longitude = 37.6173, Weight = 50, Volume = 0.5,
                Priority = OrderPriority.Normal, Status = OrderStatus.Delivered,
                DeliveryPrice = 1500, GoodsValue = 12000, IsPaid = true,
                DeliveryDate = today.AddDays(-2), CompletedAt = today.AddDays(-2).AddHours(11)
            },
            new() {
                OrderNumber = "ORD-0002", ClientName = "ИП Смирнов А.В.", ClientPhone = "+7 (495) 200-20-02",
                Address = "г. Москва, Ленинградский пр-т, д. 80",
                Latitude = 55.8094, Longitude = 37.5119, Weight = 120, Volume = 1.2,
                Priority = OrderPriority.High, Status = OrderStatus.Delivered,
                DeliveryPrice = 2800, GoodsValue = 45000, IsPaid = true,
                DeliveryDate = today.AddDays(-2), CompletedAt = today.AddDays(-2).AddHours(14)
            },
            new() {
                OrderNumber = "ORD-0003", ClientName = "ЗАО «Бета»", ClientPhone = "+7 (495) 300-30-03",
                Address = "г. Москва, ул. Арбат, д. 25",
                Latitude = 55.7494, Longitude = 37.5925, Weight = 30, Volume = 0.3,
                Priority = OrderPriority.Normal, Status = OrderStatus.Delivered,
                DeliveryPrice = 1200, GoodsValue = 8500, IsPaid = true,
                DeliveryDate = today.AddDays(-1), CompletedAt = today.AddDays(-1).AddHours(10)
            },
            new() {
                OrderNumber = "ORD-0004", ClientName = "ООО «Гамма»", ClientPhone = "+7 (495) 400-40-04",
                Address = "г. Москва, Кутузовский пр-т, д. 36",
                Latitude = 55.7404, Longitude = 37.5347, Weight = 200, Volume = 2.0,
                Priority = OrderPriority.Urgent, Status = OrderStatus.Delivered,
                DeliveryPrice = 4500, GoodsValue = 78000, CashOnDelivery = 78000, IsPaid = true,
                TimeWindowStart = new TimeSpan(9, 0, 0), TimeWindowEnd = new TimeSpan(12, 0, 0),
                DeliveryDate = today.AddDays(-1), CompletedAt = today.AddDays(-1).AddHours(11.5)
            },
            // === В ПРОЦЕССЕ (сегодня, назначен маршрут) ===
            new() {
                OrderNumber = "ORD-0005", ClientName = "ИП Кузнецов Р.Г.", ClientPhone = "+7 (495) 500-50-05",
                Address = "г. Москва, ул. Бауманская, д. 58",
                Latitude = 55.7720, Longitude = 37.6780, Weight = 75, Volume = 0.8,
                Priority = OrderPriority.Normal, Status = OrderStatus.InTransit,
                DeliveryPrice = 1800, GoodsValue = 22000, IsPaid = false,
                DeliveryDate = today, AssignedDriverId = driver1.Id
            },
            new() {
                OrderNumber = "ORD-0006", ClientName = "ООО «Дельта»", ClientPhone = "+7 (495) 600-60-06",
                Address = "г. Москва, Профсоюзная ул., д. 65",
                Latitude = 55.6544, Longitude = 37.5637, Weight = 45, Volume = 0.4,
                Priority = OrderPriority.Low, Status = OrderStatus.Assigned,
                DeliveryPrice = 1100, GoodsValue = 5600, IsPaid = false,
                DeliveryDate = today, AssignedDriverId = driver2.Id
            },
            new() {
                OrderNumber = "ORD-0007", ClientName = "АО «Эпсилон»", ClientPhone = "+7 (495) 700-70-07",
                Address = "г. Москва, Варшавское ш., д. 125",
                Latitude = 55.6318, Longitude = 37.6200, Weight = 300, Volume = 3.5,
                Priority = OrderPriority.High, Status = OrderStatus.Assigned,
                DeliveryPrice = 6000, GoodsValue = 135000, IsPaid = false,
                TimeWindowStart = new TimeSpan(14, 0, 0), TimeWindowEnd = new TimeSpan(18, 0, 0),
                DeliveryDate = today, AssignedDriverId = driver3.Id
            },
            // === НОВЫЕ (ожидают назначения) ===
            new() {
                OrderNumber = "ORD-0008", ClientName = "ИП Морозов Д.С.", ClientPhone = "+7 (495) 800-80-08",
                Address = "г. Москва, Щёлковское ш., д. 100",
                Latitude = 55.8007, Longitude = 37.7960, Weight = 90, Volume = 1.0,
                Priority = OrderPriority.Normal, Status = OrderStatus.New,
                DeliveryPrice = 2200, GoodsValue = 31000, IsPaid = false,
                DeliveryDate = today
            },
            new() {
                OrderNumber = "ORD-0009", ClientName = "ООО «Зета»", ClientPhone = "+7 (495) 900-90-09",
                Address = "г. Москва, ул. Новый Арбат, д. 15",
                Latitude = 55.7522, Longitude = 37.5870, Weight = 60, Volume = 0.6,
                Priority = OrderPriority.Normal, Status = OrderStatus.New,
                DeliveryPrice = 1600, GoodsValue = 18000, IsPaid = false,
                DeliveryDate = today.AddDays(1)
            },
            new() {
                OrderNumber = "ORD-0010", ClientName = "ИП Волков К.Н.", ClientPhone = "+7 (495) 010-01-10",
                Address = "г. Москва, Ленинский пр-т, д. 42",
                Latitude = 55.7063, Longitude = 37.5862, Weight = 150, Volume = 1.5,
                Priority = OrderPriority.High, Status = OrderStatus.New,
                DeliveryPrice = 3500, GoodsValue = 67000, IsPaid = false,
                DeliveryDate = today.AddDays(1)
            },
            new() {
                OrderNumber = "ORD-0011", ClientName = "ООО «Техснаб»", ClientPhone = "+7 (495) 111-22-33",
                Address = "г. Москва, Дмитровское ш., д. 58",
                Latitude = 55.8450, Longitude = 37.5693, Weight = 420, Volume = 5.0,
                Priority = OrderPriority.Urgent, Status = OrderStatus.New,
                DeliveryPrice = 8500, GoodsValue = 210000, CashOnDelivery = 210000, IsPaid = false,
                DeliveryDate = today.AddDays(1)
            },
            new() {
                OrderNumber = "ORD-0012", ClientName = "ИП Захарова Е.А.", ClientPhone = "+7 (495) 444-55-66",
                Address = "г. Москва, Рязанский пр-т, д. 30",
                Latitude = 55.7220, Longitude = 37.7551, Weight = 25, Volume = 0.2,
                Priority = OrderPriority.Low, Status = OrderStatus.New,
                DeliveryPrice = 900, GoodsValue = 3200, IsPaid = false,
                DeliveryDate = today.AddDays(2)
            },
        };

        await context.Orders.AddRangeAsync(orders);
        await context.SaveChangesAsync();

        // ──────────── МАРШРУТЫ (исторические + сегодняшний) ────────────
        // Маршрут 1 — завершённый (2 дня назад)
        var route1 = new Route
        {
            Name = "Маршрут №1 — Центр",
            DriverId = driver1.Id, VehicleId = vehicle1.Id,
            Date = today.AddDays(-2), Status = RouteStatus.Completed,
            TotalDistanceKm = 38.5, TotalTimeMinutes = 95,
            OptimizationMethod = "NearestNeighbor", FuelCostEstimate = 4.8
        };
        await context.Routes.AddAsync(route1);
        await context.SaveChangesAsync();

        // Точки маршрута 1 (заказы 1 и 2 — уже Delivered)
        var wp1 = new Waypoint { RouteId = route1.Id, OrderId = orders[0].Id, SequenceNumber = 1, Latitude = orders[0].Latitude, Longitude = orders[0].Longitude, Status = OrderStatus.Delivered, EstimatedArrival = today.AddDays(-2).AddHours(10), ActualArrival = today.AddDays(-2).AddHours(10.2) };
        var wp2 = new Waypoint { RouteId = route1.Id, OrderId = orders[1].Id, SequenceNumber = 2, Latitude = orders[1].Latitude, Longitude = orders[1].Longitude, Status = OrderStatus.Delivered, EstimatedArrival = today.AddDays(-2).AddHours(11), ActualArrival = today.AddDays(-2).AddHours(11.1) };
        await context.Waypoints.AddRangeAsync(wp1, wp2);
        await context.SaveChangesAsync();

        // Маршрут 2 — завершённый (1 день назад)
        var route2 = new Route
        {
            Name = "Маршрут №2 — Запад",
            DriverId = driver1.Id, VehicleId = vehicle1.Id,
            Date = today.AddDays(-1), Status = RouteStatus.Completed,
            TotalDistanceKm = 27.2, TotalTimeMinutes = 72,
            OptimizationMethod = "TwoOpt", FuelCostEstimate = 3.4
        };
        await context.Routes.AddAsync(route2);
        await context.SaveChangesAsync();

        var wp3 = new Waypoint { RouteId = route2.Id, OrderId = orders[2].Id, SequenceNumber = 1, Latitude = orders[2].Latitude, Longitude = orders[2].Longitude, Status = OrderStatus.Delivered, EstimatedArrival = today.AddDays(-1).AddHours(9), ActualArrival = today.AddDays(-1).AddHours(9.13) };
        var wp4 = new Waypoint { RouteId = route2.Id, OrderId = orders[3].Id, SequenceNumber = 2, Latitude = orders[3].Latitude, Longitude = orders[3].Longitude, Status = OrderStatus.Delivered, EstimatedArrival = today.AddDays(-1).AddHours(10), ActualArrival = today.AddDays(-1).AddHours(10.4) };
        await context.Waypoints.AddRangeAsync(wp3, wp4);
        await context.SaveChangesAsync();

        // Маршрут 3 — активный сегодня (InProgress)
        var route3 = new Route
        {
            Name = "Маршрут №3 — Юг",
            DriverId = driver2.Id, VehicleId = vehicle2.Id,
            Date = today, Status = RouteStatus.InProgress,
            TotalDistanceKm = 45.8, TotalTimeMinutes = 115,
            OptimizationMethod = "NearestNeighbor", FuelCostEstimate = 10.1
        };
        await context.Routes.AddAsync(route3);
        await context.SaveChangesAsync();

        var wp5 = new Waypoint { RouteId = route3.Id, OrderId = orders[4].Id, SequenceNumber = 1, Latitude = orders[4].Latitude, Longitude = orders[4].Longitude, Status = OrderStatus.InTransit, EstimatedArrival = today.AddHours(9) };
        var wp6 = new Waypoint { RouteId = route3.Id, OrderId = orders[5].Id, SequenceNumber = 2, Latitude = orders[5].Latitude, Longitude = orders[5].Longitude, Status = OrderStatus.Assigned,  EstimatedArrival = today.AddHours(11) };
        var wp7 = new Waypoint { RouteId = route3.Id, OrderId = orders[6].Id, SequenceNumber = 3, Latitude = orders[6].Latitude, Longitude = orders[6].Longitude, Status = OrderStatus.Assigned,  EstimatedArrival = today.AddHours(14) };
        await context.Waypoints.AddRangeAsync(wp5, wp6, wp7);
        await context.SaveChangesAsync();
    }
}
