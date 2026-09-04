using Genlogs.Api.Data;
using Genlogs.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Genlogs.Api.Services;

public class LaneResolutionService : ILaneResolutionService
{
    private readonly GenlogsDbContext _db;

    public LaneResolutionService(GenlogsDbContext db)
    {
        _db = db;
    }

    public async Task<Lane> ResolveLaneAsync(string origin, string destination, CancellationToken cancellationToken = default)
    {
        var originKey = CityNormalizer.Normalize(origin);
        var destinationKey = CityNormalizer.Normalize(destination);

        // "does not match a known lane (in either direction)" (spec.md) — a lane serves trucks moving
        // both ways, so a reversed origin/destination pair still counts as a match.
        var matchedLane = await _db.Lanes
            .Where(l => !l.IsDefaultFallback)
            .Where(l =>
                (l.OriginCity == originKey && l.DestinationCity == destinationKey) ||
                (l.OriginCity == destinationKey && l.DestinationCity == originKey))
            .FirstOrDefaultAsync(cancellationToken);

        if (matchedLane is not null)
        {
            return matchedLane;
        }

        var fallbackLane = await _db.Lanes
            .FirstOrDefaultAsync(l => l.IsDefaultFallback, cancellationToken);

        return fallbackLane
            ?? throw new InvalidOperationException("Default fallback lane is not seeded; database initialization is incomplete.");
    }
}
