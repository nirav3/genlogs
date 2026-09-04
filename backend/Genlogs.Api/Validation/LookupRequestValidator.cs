using FluentValidation;
using Genlogs.Api.Models.Dtos;

namespace Genlogs.Api.Validation;

public class LookupRequestValidator : AbstractValidator<LookupRequest>
{
    public LookupRequestValidator()
    {
        RuleFor(x => x.Origin)
            .NotEmpty().WithMessage("Origin is required.");

        RuleFor(x => x.Destination)
            .NotEmpty().WithMessage("Destination is required.");
    }
}
