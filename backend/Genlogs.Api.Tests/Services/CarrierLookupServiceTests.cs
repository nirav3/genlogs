using Genlogs.Api.Data;
using Genlogs.Api.Models;
using Genlogs.Api.Services;
using Genlogs.Api.Tests.TestSupport;
using Xunit;

namespace Genlogs.Api.Tests.Services;

public class CarrierLookupServiceTests
{
    [Fact]
    public async Task GetRankedCarriersAsync_HandCraftedDetections_ComputesExpectedAverage()
    {
        using var db = new SqliteTestDatabase();

        var lane = new Lane { OriginCity = "a", DestinationCity = "b", IsDefaultFallback = false };
        var carrier = new Carrier { Name = "Test Carrier", UsdotNumber = "000001" };
        var vehicle1 = new Vehicle { PlateNumber = "V1", Carrier = carrier };
        var vehicle2 = new Vehicle { PlateNumber = "V2", Carrier = carrier };
        db.Context.AddRange(lane, carrier, vehicle1, vehicle2);

        // Day 1: 2 distinct vehicles. Day 2: 1 distinct vehicle (vehicle1 detected twice, still 1 distinct).
        var day1 = new DateOnly(2026, 1, 1);
        var day2 = new DateOnly(2026, 1, 2);
        db.Context.DetectionEvents.AddRange(
            new DetectionEvent { Lane = lane, Vehicle = vehicle1, CapturedAt = day1 },
            new DetectionEvent { Lane = lane, Vehicle = vehicle2, CapturedAt = day1 },
            new DetectionEvent { Lane = lane, Vehicle = vehicle1, CapturedAt = day2 },
            new DetectionEvent { Lane = lane, Vehicle = vehicle1, CapturedAt = day2 });
        db.Context.SaveChanges();

        var service = new CarrierLookupService(db.Context);
        var results = await service.GetRankedCarriersAsync(lane.LaneId);

        var result = Assert.Single(results);
        Assert.Equal("Test Carrier", result.Name);
        // (2 + 1) / 2 days = 1.5
        Assert.Equal(1.5, result.TrucksPerDay);
    }

    [Fact]
    public async Task GetRankedCarriersAsync_NoDetectionsForLane_ReturnsEmptyList()
    {
        using var db = new SqliteTestDatabase();
        var lane = new Lane { OriginCity = "a", DestinationCity = "b", IsDefaultFallback = false };
        db.Context.Lanes.Add(lane);
        db.Context.SaveChanges();

        var service = new CarrierLookupService(db.Context);
        var results = await service.GetRankedCarriersAsync(lane.LaneId);

        Assert.Empty(results);
    }

    [Fact]
    public async Task GetRankedCarriersAsync_NycToDcLane_ReturnsDocumentedCarriersInDescendingOrder()
    {
        using var db = new SqliteTestDatabase();
        DbInitializer.Seed(db.Context);
        var laneResolver = new LaneResolutionService(db.Context);
        var lane = await laneResolver.ResolveLaneAsync("New York City", "Washington, DC");

        var service = new CarrierLookupService(db.Context);
        var results = await service.GetRankedCarriersAsync(lane.LaneId);

        Assert.Equal(3, results.Count);
        Assert.Equal(("Knight-Swift Transport Services", 10d), (results[0].Name, results[0].TrucksPerDay));
        Assert.Equal(("J.B. Hunt Transport Services Inc", 7d), (results[1].Name, results[1].TrucksPerDay));
        Assert.Equal(("YRC Worldwide", 5d), (results[2].Name, results[2].TrucksPerDay));
    }

    [Fact]
    public async Task GetRankedCarriersAsync_SfToLaLane_ReturnsDocumentedCarriersInDescendingOrder()
    {
        using var db = new SqliteTestDatabase();
        DbInitializer.Seed(db.Context);
        var laneResolver = new LaneResolutionService(db.Context);
        var lane = await laneResolver.ResolveLaneAsync("San Francisco", "Los Angeles");

        var service = new CarrierLookupService(db.Context);
        var results = await service.GetRankedCarriersAsync(lane.LaneId);

        Assert.Equal(3, results.Count);
        Assert.Equal(("XPO Logistics", 9d), (results[0].Name, results[0].TrucksPerDay));
        Assert.Equal(("Schneider", 6d), (results[1].Name, results[1].TrucksPerDay));
        Assert.Equal(("Landstar Systems", 2d), (results[2].Name, results[2].TrucksPerDay));
    }

    [Fact]
    public async Task GetRankedCarriersAsync_DefaultFallbackLane_ReturnsUpsThenFedEx()
    {
        using var db = new SqliteTestDatabase();
        DbInitializer.Seed(db.Context);
        var laneResolver = new LaneResolutionService(db.Context);
        var lane = await laneResolver.ResolveLaneAsync("Chicago", "Denver");

        var service = new CarrierLookupService(db.Context);
        var results = await service.GetRankedCarriersAsync(lane.LaneId);

        Assert.Equal(2, results.Count);
        Assert.Equal(("UPS Inc.", 11d), (results[0].Name, results[0].TrucksPerDay));
        Assert.Equal(("FedEx Corp", 9d), (results[1].Name, results[1].TrucksPerDay));
    }

    [Fact]
    public async Task GetRankedCarriersAsync_CalledTwiceWithoutDataChange_ReturnsSameFigures()
    {
        using var db = new SqliteTestDatabase();
        DbInitializer.Seed(db.Context);
        var laneResolver = new LaneResolutionService(db.Context);
        var lane = await laneResolver.ResolveLaneAsync("San Francisco", "Los Angeles");
        var service = new CarrierLookupService(db.Context);

        var first = await service.GetRankedCarriersAsync(lane.LaneId);
        var second = await service.GetRankedCarriersAsync(lane.LaneId);

        Assert.Equal(first, second);
    }
}
