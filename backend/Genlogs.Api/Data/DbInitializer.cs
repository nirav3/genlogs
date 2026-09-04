using Genlogs.Api.Models;
using Genlogs.Api.Services;

namespace Genlogs.Api.Data;

/// <summary>
/// Seeds synthetic lane/carrier/vehicle/detection data on a fresh (empty) database, per design.md's
/// deterministic ±1-around-target calibration so each carrier's 7-day average lands exactly on the
/// figures in requirements.md / data/carriers.mock.json.
/// </summary>
public static class DbInitializer
{
    public static void Seed(GenlogsDbContext db)
    {
        if (db.Carriers.Any())
        {
            return;
        }

        var referenceDate = DateOnly.FromDateTime(DateTime.UtcNow);

        var nycToDc = new Lane
        {
            OriginCity = CityNormalizer.Canonical.NewYorkCity,
            DestinationCity = CityNormalizer.Canonical.WashingtonDc,
            IsDefaultFallback = false,
        };
        var sfToLa = new Lane
        {
            OriginCity = CityNormalizer.Canonical.SanFrancisco,
            DestinationCity = CityNormalizer.Canonical.LosAngeles,
            IsDefaultFallback = false,
        };
        var defaultFallback = new Lane
        {
            OriginCity = CityNormalizer.Canonical.DefaultFallback,
            DestinationCity = CityNormalizer.Canonical.DefaultFallback,
            IsDefaultFallback = true,
        };

        db.Lanes.AddRange(nycToDc, sfToLa, defaultFallback);

        SeedCarrierOnLane(db, nycToDc, "Knight-Swift Transport Services", "931793", targetTrucksPerDay: 10, referenceDate);
        SeedCarrierOnLane(db, nycToDc, "J.B. Hunt Transport Services Inc", "509880", targetTrucksPerDay: 7, referenceDate);
        SeedCarrierOnLane(db, nycToDc, "YRC Worldwide", "066142", targetTrucksPerDay: 5, referenceDate);

        SeedCarrierOnLane(db, sfToLa, "XPO Logistics", "1362822", targetTrucksPerDay: 9, referenceDate);
        SeedCarrierOnLane(db, sfToLa, "Schneider", "070851", targetTrucksPerDay: 6, referenceDate);
        SeedCarrierOnLane(db, sfToLa, "Landstar Systems", "148444", targetTrucksPerDay: 2, referenceDate);

        SeedCarrierOnLane(db, defaultFallback, "UPS Inc.", "022216", targetTrucksPerDay: 11, referenceDate);
        SeedCarrierOnLane(db, defaultFallback, "FedEx Corp", "086395", targetTrucksPerDay: 9, referenceDate);

        db.SaveChanges();
    }

    private static void SeedCarrierOnLane(
        GenlogsDbContext db,
        Lane lane,
        string carrierName,
        string usdotNumber,
        int targetTrucksPerDay,
        DateOnly referenceDate)
    {
        var carrier = new Carrier { Name = carrierName, UsdotNumber = usdotNumber };
        db.Carriers.Add(carrier);

        var dailyCounts = BuildDeterministicDailyPattern(targetTrucksPerDay);
        var fleetSize = dailyCounts.Max();

        // A fixed synthetic fleet reused across days, per design.md — a truck running the lane daily is
        // one distinct vehicle, not a new sighting-day each time.
        var fleet = Enumerable.Range(1, fleetSize)
            .Select(seq => new Vehicle { PlateNumber = $"{usdotNumber}-{seq:D2}", Carrier = carrier })
            .ToList();
        db.Vehicles.AddRange(fleet);

        for (var dayIndex = 0; dayIndex < dailyCounts.Length; dayIndex++)
        {
            var day = referenceDate.AddDays(dayIndex - (dailyCounts.Length - 1));
            foreach (var vehicle in fleet.Take(dailyCounts[dayIndex]))
            {
                db.DetectionEvents.Add(new DetectionEvent { Lane = lane, Vehicle = vehicle, CapturedAt = day });
            }
        }
    }

    // design.md: "alternates one below and one above its target across the 7 seeded days" — e.g. target
    // 10: 9, 11, 9, 11, 9, 11, 10 -> sums to 70, averages to exactly 10.
    internal static int[] BuildDeterministicDailyPattern(int target) =>
        new[] { target - 1, target + 1, target - 1, target + 1, target - 1, target + 1, target };
}
