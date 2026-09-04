using Genlogs.Api.Services;
using Xunit;

namespace Genlogs.Api.Tests.Services;

public class CityNormalizerTests
{
    [Theory]
    [InlineData("New York City", "new york city")]
    [InlineData("new york city", "new york city")]
    [InlineData(" NYC ", "new york city")]
    [InlineData("nyc", "new york city")]
    [InlineData("New York City, NY", "new york city")]
    [InlineData("Washington, DC", "washington dc")]
    [InlineData("Washington DC", "washington dc")]
    [InlineData("  washington,   dc  ", "washington dc")]
    [InlineData("San Francisco", "san francisco")]
    [InlineData("San Francisco, CA", "san francisco")]
    [InlineData("Los Angeles", "los angeles")]
    [InlineData("Los Angeles, CA", "los angeles")]
    public void Normalize_MatchesCanonicalKey_RegardlessOfCaseWhitespaceOrStateQualifier(string input, string expected)
    {
        Assert.Equal(expected, CityNormalizer.Normalize(input));
    }

    [Fact]
    public void Normalize_UnknownCity_ReturnsItsOwnNormalizedForm()
    {
        Assert.Equal("chicago", CityNormalizer.Normalize("Chicago"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalize_NullOrWhitespace_ReturnsEmptyString(string? input)
    {
        Assert.Equal(string.Empty, CityNormalizer.Normalize(input));
    }
}
