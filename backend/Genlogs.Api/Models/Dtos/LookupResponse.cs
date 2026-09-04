namespace Genlogs.Api.Models.Dtos;

public record CarrierResult(string Name, double TrucksPerDay);

public record LookupResponse(IReadOnlyList<CarrierResult> Carriers);
