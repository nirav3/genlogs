using FluentValidation;
using Genlogs.Api.Models.Dtos;
using Genlogs.Api.Services;

namespace Genlogs.Api.Endpoints;

public static class CarrierEndpoints
{
    public static void MapCarrierEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/carriers").WithTags("Carriers");

        group.MapPost("/lookup", async (
                LookupRequest request,
                IValidator<LookupRequest> validator,
                ILaneResolutionService laneResolutionService,
                ICarrierLookupService carrierLookupService,
                CancellationToken cancellationToken) =>
            {
                var validationResult = await validator.ValidateAsync(request, cancellationToken);
                if (!validationResult.IsValid)
                {
                    return Results.ValidationProblem(validationResult.ToDictionary());
                }

                var lane = await laneResolutionService.ResolveLaneAsync(request.Origin!, request.Destination!, cancellationToken);
                var carriers = await carrierLookupService.GetRankedCarriersAsync(lane.LaneId, cancellationToken);

                return Results.Ok(new LookupResponse(carriers));
            })
            .RequireAuthorization()
            .RequireRateLimiting(RateLimiterPolicies.Lookup);
    }
}
