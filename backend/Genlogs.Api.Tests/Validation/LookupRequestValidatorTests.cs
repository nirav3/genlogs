using Genlogs.Api.Models.Dtos;
using Genlogs.Api.Validation;
using Xunit;

namespace Genlogs.Api.Tests.Validation;

public class LookupRequestValidatorTests
{
    private readonly LookupRequestValidator _validator = new();

    [Fact]
    public void Validate_ValidRequest_Passes()
    {
        var result = _validator.Validate(new LookupRequest("New York City", "Washington, DC"));
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_MissingOrigin_Fails()
    {
        var result = _validator.Validate(new LookupRequest(null, "Washington, DC"));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(LookupRequest.Origin));
    }

    [Fact]
    public void Validate_MissingDestination_Fails()
    {
        var result = _validator.Validate(new LookupRequest("New York City", null));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(LookupRequest.Destination));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_EmptyOrWhitespaceOrigin_Fails(string origin)
    {
        var result = _validator.Validate(new LookupRequest(origin, "Washington, DC"));
        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_EmptyOrWhitespaceDestination_Fails(string destination)
    {
        var result = _validator.Validate(new LookupRequest("New York City", destination));
        Assert.False(result.IsValid);
    }
}
