using Genlogs.Api.Startup;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Genlogs.Api.Tests.Startup;

public class ConfigurationGuardTests
{
    [Fact]
    public void EnsureRequiredConfiguration_GoogleClientIdPresent_DoesNotThrow()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["GOOGLE_CLIENT_ID"] = "some-client-id" })
            .Build();

        var exception = Record.Exception(() => ConfigurationGuard.EnsureRequiredConfiguration(configuration));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureRequiredConfiguration_GoogleClientIdMissing_Throws()
    {
        var configuration = new ConfigurationBuilder().Build();

        Assert.Throws<InvalidOperationException>(() => ConfigurationGuard.EnsureRequiredConfiguration(configuration));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EnsureRequiredConfiguration_GoogleClientIdEmptyOrWhitespace_Throws(string value)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["GOOGLE_CLIENT_ID"] = value })
            .Build();

        Assert.Throws<InvalidOperationException>(() => ConfigurationGuard.EnsureRequiredConfiguration(configuration));
    }
}
