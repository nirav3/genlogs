using Genlogs.Api.Data;
using Genlogs.Api.Services;
using Genlogs.Api.Tests.TestSupport;
using Xunit;

namespace Genlogs.Api.Tests.Services;

public class LaneResolutionServiceTests
{
    [Theory]
    [InlineData("New York City", "Washington, DC")]
    [InlineData("NYC", "Washington DC")]
    [InlineData(" nyc ", " washington, dc ")]
    [InlineData("New York City, NY", "Washington, DC")]
    public async Task ResolveLaneAsync_NycToDcVariants_ResolvesToNycDcLane(string origin, string destination)
    {
        using var db = new SqliteTestDatabase();
        DbInitializer.Seed(db.Context);
        var service = new LaneResolutionService(db.Context);

        var lane = await service.ResolveLaneAsync(origin, destination);

        Assert.False(lane.IsDefaultFallback);
        Assert.Equal(CityNormalizer.Canonical.NewYorkCity, lane.OriginCity);
        Assert.Equal(CityNormalizer.Canonical.WashingtonDc, lane.DestinationCity);
    }

    [Theory]
    [InlineData("San Francisco", "Los Angeles")]
    [InlineData("San Francisco, CA", "Los Angeles, CA")]
    public async Task ResolveLaneAsync_SfToLaVariants_ResolvesToSfLaLane(string origin, string destination)
    {
        using var db = new SqliteTestDatabase();
        DbInitializer.Seed(db.Context);
        var service = new LaneResolutionService(db.Context);

        var lane = await service.ResolveLaneAsync(origin, destination);

        Assert.False(lane.IsDefaultFallback);
        Assert.Equal(CityNormalizer.Canonical.SanFrancisco, lane.OriginCity);
        Assert.Equal(CityNormalizer.Canonical.LosAngeles, lane.DestinationCity);
    }

    [Fact]
    public async Task ResolveLaneAsync_ReversedKnownLane_StillResolvesToThatLane()
    {
        using var db = new SqliteTestDatabase();
        DbInitializer.Seed(db.Context);
        var service = new LaneResolutionService(db.Context);

        var lane = await service.ResolveLaneAsync("Washington, DC", "New York City");

        Assert.False(lane.IsDefaultFallback);
        Assert.Equal(CityNormalizer.Canonical.NewYorkCity, lane.OriginCity);
        Assert.Equal(CityNormalizer.Canonical.WashingtonDc, lane.DestinationCity);
    }

    [Fact]
    public async Task ResolveLaneAsync_UnmatchedPair_FallsBackToDefaultLane()
    {
        using var db = new SqliteTestDatabase();
        DbInitializer.Seed(db.Context);
        var service = new LaneResolutionService(db.Context);

        var lane = await service.ResolveLaneAsync("Chicago", "Denver");

        Assert.True(lane.IsDefaultFallback);
    }
}
