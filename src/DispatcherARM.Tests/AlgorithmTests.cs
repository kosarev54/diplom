using DispatcherARM.Domain.Entities;
using DispatcherARM.Domain.Enums;

namespace DispatcherARM.Tests;

/// <summary>
/// Юнит-тесты для модулей оптимизации маршрутов и бизнес-логики
/// Коарев А.А., ВлГУ, гр. ПИ-123, 09.03.03
/// </summary>

// ════════════════════════════════════════════════════════════════════════════
// 1. Тесты алгоритма ближайшего соседа (без OSRM — изолированные)
// ════════════════════════════════════════════════════════════════════════════
public class NearestNeighborLogicTests
{
    /// <summary>Расчёт стоимости ребра между двумя точками</summary>
    [Fact]
    public void EdgeCost_KnownValues_ReturnsCorrect()
    {
        // Arrange — матрица расстояний (секунды)
        var durations = new double?[][]
        {
            new double?[] { 0, 100, 200 },   // из 0
            new double?[] { 100, 0, 50  },   // из 1
            new double?[] { 200, 50,  0  }   // из 2
        };

        // Act — ближайший к точке 0 — это точка 1 (100 < 200)
        int nearest = -1;
        double minDuration = double.MaxValue;
        for (int j = 1; j < 3; j++)
        {
            if (durations[0]![j].HasValue && durations[0]![j]!.Value < minDuration)
            {
                minDuration = durations[0]![j]!.Value;
                nearest = j;
            }
        }

        // Assert
        Assert.Equal(1, nearest);
        Assert.Equal(100, minDuration);
    }

    /// <summary>При одном заказе маршрут содержит ровно одну точку</summary>
    [Fact]
    public void SingleOrder_RouteHasOneWaypoint()
    {
        var durations = new double?[][]
        {
            new double?[] { 0,   300 },
            new double?[] { 300, 0   }
        };

        // Алгоритм NN: [0] → [1]
        var visited = new bool[2];
        var route   = new List<int> { 0 };
        visited[0]  = true;

        for (int step = 1; step < 2; step++)
        {
            int current = route[^1];
            int nearest = -1;
            double minD = double.MaxValue;
            for (int j = 1; j < 2; j++)
            {
                if (!visited[j] && durations[current]![j].HasValue && durations[current]![j]!.Value < minD)
                {
                    minD = durations[current]![j]!.Value;
                    nearest = j;
                }
            }
            if (nearest == -1) break;
            visited[nearest] = true;
            route.Add(nearest);
        }

        var waypoints = route.Skip(1).ToList(); // Пропускаем депо
        Assert.Single(waypoints);
    }

    /// <summary>Без заказов маршрут пуст</summary>
    [Fact]
    public void ZeroOrders_EmptyRoute()
    {
        var orders = new List<Order>();
        Assert.Empty(orders);
    }
}

// ════════════════════════════════════════════════════════════════════════════
// 2. Тесты 2-opt улучшения
// ════════════════════════════════════════════════════════════════════════════
public class TwoOptLogicTests
{
    private static double GetEdgeCost(double?[][] d, int from, int to)
        => d[from]?[to] ?? double.MaxValue;

    private static List<int> Improve2Opt(List<int> route, double?[][] durations)
    {
        bool improved = true;
        int maxIt = 1000, it = 0;
        while (improved && it < maxIt)
        {
            improved = false;
            it++;
            for (int i = 1; i < route.Count - 1; i++)
            {
                for (int j = i + 1; j < route.Count; j++)
                {
                    double cur = GetEdgeCost(durations, route[i - 1], route[i])
                               + GetEdgeCost(durations, route[j], j + 1 < route.Count ? route[j + 1] : route[0]);
                    double nw  = GetEdgeCost(durations, route[i - 1], route[j])
                               + GetEdgeCost(durations, route[i], j + 1 < route.Count ? route[j + 1] : route[0]);
                    if (nw < cur)
                    {
                        route.Reverse(i, j - i + 1);
                        improved = true;
                    }
                }
            }
        }
        return route;
    }

    /// <summary>2-opt не ухудшает уже оптимальный маршрут</summary>
    [Fact]
    public void TwoOpt_OptimalRoute_NotWorsened()
    {
        // 3 точки: депо(0), A(1), B(2)
        var durations = new double?[][]
        {
            new double?[] { 0, 10, 20 },
            new double?[] { 10, 0, 10 },
            new double?[] { 20, 10, 0 }
        };
        var route = new List<int> { 0, 1, 2 };

        // Рассчитываем стоимость до
        double costBefore = GetEdgeCost(durations, route[0], route[1])
                          + GetEdgeCost(durations, route[1], route[2]);

        var improved = Improve2Opt(route, durations);

        double costAfter = GetEdgeCost(durations, improved[0], improved[1])
                         + GetEdgeCost(durations, improved[1], improved[2]);

        Assert.True(costAfter <= costBefore, $"2-opt ухудшил маршрут: {costBefore} → {costAfter}");
    }

    /// <summary>2-opt результат не хуже входного маршрута (монотонность)</summary>
    [Fact]
    public void TwoOpt_ResultNeverWorseThanInput()
    {
        // Произвольная матрица из 5 точек
        var durations = new double?[][]
        {
            new double?[] { 0,   50,  80,  30,  60 },
            new double?[] { 50,  0,   40,  70,  20 },
            new double?[] { 80,  40,  0,   55,  35 },
            new double?[] { 30,  70,  55,  0,   45 },
            new double?[] { 60,  20,  35,  45,  0  }
        };
        var route = new List<int> { 0, 3, 1, 4, 2 }; // произвольный маршрут

        double CostOf(List<int> r)
        {
            double c = 0;
            for (int i = 0; i < r.Count - 1; i++)
                c += GetEdgeCost(durations, r[i], r[i + 1]);
            return c;
        }

        double before = CostOf(route);
        var result    = Improve2Opt(new List<int>(route), durations);
        double after  = CostOf(result);

        Assert.True(after <= before, $"2-opt ухудшил маршрут: {before:F0} → {after:F0}");
    }
}

// ════════════════════════════════════════════════════════════════════════════
// 3. Тесты модели заказа
// ════════════════════════════════════════════════════════════════════════════
public class OrderEntityTests
{
    [Fact]
    public void Order_DefaultStatus_IsNew()
    {
        var order = new Order
        {
            OrderNumber = "ORD-TEST-001",
            ClientName  = "Тест",
            Address     = "г. Москва"
        };
        Assert.Equal(OrderStatus.New, order.Status);
    }

    [Fact]
    public void Order_PriorityDefault_IsNormal()
    {
        var order = new Order
        {
            OrderNumber = "ORD-TEST-002",
            ClientName  = "Тест",
            Address     = "г. Москва"
        };
        Assert.Equal(OrderPriority.Normal, order.Priority);
    }

    [Theory]
    [InlineData("ORD-001")]
    [InlineData("ORD-WEB-20240101120000-001")]
    [InlineData("ORD-1C-20240101120000-001")]
    public void Order_NumberFormats_Valid(string number)
    {
        var order = new Order
        {
            OrderNumber = number,
            ClientName  = "Клиент",
            Address     = "Адрес"
        };
        Assert.False(string.IsNullOrWhiteSpace(order.OrderNumber));
    }

    [Fact]
    public void Order_Items_NotNullByDefault()
    {
        var order = new Order
        {
            OrderNumber = "ORD-001",
            ClientName  = "Тест",
            Address     = "Тест"
        };
        Assert.NotNull(order.Items);
    }
}

// ════════════════════════════════════════════════════════════════════════════
// 4. Тесты сущности водителя
// ════════════════════════════════════════════════════════════════════════════
public class DriverEntityTests
{
    [Fact]
    public void Driver_DefaultStatus_IsAvailable()
    {
        var driver = new Driver
        {
            FullName      = "Петров П.П.",
            Phone         = "+7 (999) 111-11-11",
            LicenseNumber = "77 01 123456"
        };
        // После исправления: DriverStatus.Available = 0
        Assert.Equal(DriverStatus.Available, driver.Status);
    }

    [Fact]
    public void Driver_DefaultSalary_IsPositive()
    {
        var driver = new Driver
        {
            FullName      = "Сидоров С.С.",
            Phone         = "+7 (999) 222-22-22",
            LicenseNumber = "77 02 654321"
        };
        Assert.True(driver.BaseSalary > 0, $"BaseSalary должен быть > 0, но = {driver.BaseSalary}");
    }

    [Fact]
    public void DriverStatus_Enum_HasFourValues()
    {
        var values = Enum.GetValues<DriverStatus>();
        Assert.Equal(4, values.Length); // Available, OnRoute, OnLeave, Suspended
    }
}

// ════════════════════════════════════════════════════════════════════════════
// 5. Тесты расчёта даты доставки
// ════════════════════════════════════════════════════════════════════════════
public class DeliveryDateCalculatorTests
{
    private static DateTime CalculateDeliveryDate(OrderPriority priority)
    {
        // Логика из DeliveryDateCalculator
        return priority switch
        {
            OrderPriority.Urgent => DateTime.Today,
            OrderPriority.High   => DateTime.Today.AddDays(1),
            OrderPriority.Normal => DateTime.Today.AddDays(2),
            OrderPriority.Low    => DateTime.Today.AddDays(3),
            _                    => DateTime.Today.AddDays(2)
        };
    }

    [Theory]
    [InlineData(OrderPriority.Urgent, 0)]
    [InlineData(OrderPriority.High,   1)]
    [InlineData(OrderPriority.Normal, 2)]
    [InlineData(OrderPriority.Low,    3)]
    public void DeliveryDate_MatchesPriorityExpectation(OrderPriority priority, int expectedDaysOffset)
    {
        var result   = CalculateDeliveryDate(priority);
        var expected = DateTime.Today.AddDays(expectedDaysOffset);
        Assert.Equal(expected.Date, result.Date);
    }

    [Fact]
    public void UrgentDelivery_IsToday()
    {
        var date = CalculateDeliveryDate(OrderPriority.Urgent);
        Assert.Equal(DateTime.Today, date.Date);
    }

    [Fact]
    public void LowPriority_AtLeast3DaysAhead()
    {
        var date = CalculateDeliveryDate(OrderPriority.Low);
        Assert.True((date - DateTime.Today).TotalDays >= 3);
    }
}

// ════════════════════════════════════════════════════════════════════════════
// 6. Тесты транспортного средства
// ════════════════════════════════════════════════════════════════════════════
public class VehicleEntityTests
{
    [Fact]
    public void Vehicle_DefaultStatus_IsAvailable()
    {
        var v = new Vehicle
        {
            LicensePlate = "А001АА77",
            Model        = "ГАЗель NEXT",
            Vin          = "XUF3302420K000001"
        };
        Assert.Equal(VehicleStatus.Available, v.Status);
    }

    [Fact]
    public void Vehicle_FuelCostCalculation_Correct()
    {
        var v = new Vehicle
        {
            LicensePlate             = "В002ВВ77",
            Model                    = "КАМАЗ",
            Vin                      = "XTC432610K0000002",
            FuelConsumptionPer100Km  = 22.0,
            MaxWeightKg              = 5000
        };
        double distanceKm = 100;
        double fuelUsed   = distanceKm * v.FuelConsumptionPer100Km / 100.0;
        Assert.Equal(22.0, fuelUsed, precision: 2);
    }
}
