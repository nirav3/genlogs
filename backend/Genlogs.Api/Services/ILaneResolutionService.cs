using Genlogs.Api.Models;

namespace Genlogs.Api.Services;

public interface ILaneResolutionService
{
    Task<Lane> ResolveLaneAsync(string origin, string destination, CancellationToken cancellationToken = default);
}
