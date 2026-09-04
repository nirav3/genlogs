using Genlogs.Api.Data;
using Genlogs.Api.Models.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Genlogs.Api.Services;

public class CarrierLookupService : ICarrierLookupService
{
    private readonly GenlogsDbContext _db;

    public CarrierLookupService(GenlogsDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<CarrierResult>> GetRankedCarriersAsync(int laneId, CancellationToken cancellationToken = default)
    {
        // Stage 1 (real SQL via EF Core LINQ): distinct vehicles detected per carrier per day, on this lane.
        var dailyCounts = await _db.DetectionEvents
            .Where(d => d.LaneId == laneId)
            .GroupBy(d => new { d.Vehicle.CarrierId, d.CapturedAt })
            .Select(g => new DailyCarrierCount(
                g.Key.CarrierId,
                g.Key.CapturedAt,
                g.Select(x => x.VehicleId).Distinct().Count()))
            .ToListAsync(cancellationToken);

        if (dailyCounts.Count == 0)
        {
            return Array.Empty<CarrierResult>();
        }

        var carrierIds = dailyCounts.Select(d => d.CarrierId).Distinct().ToList();
        var carrierNames = await _db.Carriers
            .Where(c => carrierIds.Contains(c.CarrierId))
            .ToDictionaryAsync(c => c.CarrierId, c => c.Name, cancellationToken);

        // Stage 2 (in-memory): average each carrier's per-day counts across the days it has data for —
        // the 7-day rolling window this represents is fixed by what DbInitializer seeds per lane.
        return dailyCounts
            .GroupBy(d => d.CarrierId)
            .Select(g => new CarrierResult(
                carrierNames.TryGetValue(g.Key, out var name) ? name : "Unknown",
                g.Average(x => x.DistinctVehicleCount)))
            .OrderByDescending(c => c.TrucksPerDay)
            .ToList();
    }

    private sealed record DailyCarrierCount(int CarrierId, DateOnly CapturedAt, int DistinctVehicleCount);
}
