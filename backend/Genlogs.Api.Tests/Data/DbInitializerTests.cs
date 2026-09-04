using Genlogs.Api.Data;
using Genlogs.Api.Models;
using Genlogs.Api.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Genlogs.Api.Tests.Data;

public class DbInitializerTests
{
    [Theory]
    [InlineData("Knight-Swift Transport Services", 10)]
    [InlineData("J.B. Hunt Transport Services Inc", 7)]
    [InlineData("YRC Worldwide", 5)]
    [InlineData("XPO Logistics", 9)]
    [InlineData("Schneider", 6)]
    [InlineData("Landstar Systems", 2)]
    [InlineData("UPS Inc.", 11)]
    [InlineData("FedEx Corp", 9)]
    public void Seed_EachCarriersSevenDayAverage_MatchesDocumentedTrucksPerDayFigure(string carrierName, double expectedTrucksPerDay)
    {
        using var db = new SqliteTestDatabase();
        DbInitializer.Seed(db.Context);

        var carrier = db.Context.Carriers.Single(c => c.Name == carrierName);

        var dailyCounts = db.Context.DetectionEvents
            .Where(d => d.Vehicle.CarrierId == carrier.CarrierId)
            .GroupBy(d => d.CapturedAt)
            .Select(g => g.Select(x => x.VehicleId).Distinct().Count())
            .ToList();

        Assert.Equal(7, dailyCounts.Count);
        Assert.Equal(expectedTrucksPerDay, dailyCounts.Average());
    }

    [Fact]
    public void Seed_SeedsExactlyOneDefaultFallbackLane_WithWildcardCities()
    {
        using var db = new SqliteTestDatabase();
        DbInitializer.Seed(db.Context);

        var fallback = Assert.Single(db.Context.Lanes.Where(l => l.IsDefaultFallback));
        Assert.Equal("*", fallback.OriginCity);
        Assert.Equal("*", fallback.DestinationCity);
    }

    [Fact]
    public void Seed_SeedsBothNamedLanes_AsNonDefault()
    {
        using var db = new SqliteTestDatabase();
        DbInitializer.Seed(db.Context);

        Assert.Equal(2, db.Context.Lanes.Count(l => !l.IsDefaultFallback));
    }

    [Fact]
    public void Seed_CalledTwice_DoesNotDuplicateRows()
    {
        using var db = new SqliteTestDatabase();
        DbInitializer.Seed(db.Context);
        var carrierCountAfterFirstSeed = db.Context.Carriers.Count();

        DbInitializer.Seed(db.Context);

        Assert.Equal(carrierCountAfterFirstSeed, db.Context.Carriers.Count());
    }

    [Fact]
    public void QueryForUnseededLanePair_FallsBackToDefaultLane()
    {
        using var db = new SqliteTestDatabase();
        DbInitializer.Seed(db.Context);

        var noExactMatch = db.Context.Lanes
            .FirstOrDefault(l => l.OriginCity == "chicago" && l.DestinationCity == "denver");
        Assert.Null(noExactMatch);

        var fallback = db.Context.Lanes.Single(l => l.IsDefaultFallback);
        Assert.NotNull(fallback);
    }
}
