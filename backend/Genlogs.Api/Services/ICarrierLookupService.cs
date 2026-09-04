using Genlogs.Api.Models.Dtos;

namespace Genlogs.Api.Services;

public interface ICarrierLookupService
{
    Task<IReadOnlyList<CarrierResult>> GetRankedCarriersAsync(int laneId, CancellationToken cancellationToken = default);
}
