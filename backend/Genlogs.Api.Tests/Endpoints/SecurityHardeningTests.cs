using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Genlogs.Api.Models.Dtos;
using Genlogs.Api.Tests.TestSupport;
using Xunit;

namespace Genlogs.Api.Tests.Endpoints;

public class SecurityHeadersTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public SecurityHeadersTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Response_IncludesSecurityHeaders()
    {
        var response = await _client.GetAsync("/health");

        Assert.True(response.Headers.Contains("X-Content-Type-Options"));
        Assert.True(response.Headers.Contains("X-Frame-Options"));
    }
}

public class CorsPolicyTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public CorsPolicyTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task PreflightFromAllowedOrigin_ReflectsThatOrigin()
    {
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/carriers/lookup");
        request.Headers.Add("Origin", CustomWebApplicationFactory.AllowedOrigin);
        request.Headers.Add("Access-Control-Request-Method", "POST");

        var response = await _client.SendAsync(request);

        Assert.True(response.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.Contains(CustomWebApplicationFactory.AllowedOrigin, response.Headers.GetValues("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task PreflightFromDisallowedOrigin_DoesNotReflectThatOrigin()
    {
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/carriers/lookup");
        request.Headers.Add("Origin", "https://not-allowed.example.com");
        request.Headers.Add("Access-Control-Request-Method", "POST");

        var response = await _client.SendAsync(request);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }
}

public class RateLimitingTests
{
    [Fact]
    public async Task ExceedingLookupRateLimit_Returns429()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestJwtTokenFactory.CreateValidToken());

        HttpResponseMessage? lastResponse = null;
        for (var i = 0; i < 31; i++)
        {
            lastResponse = await client.PostAsJsonAsync("/api/carriers/lookup", new LookupRequest("New York City", "Washington, DC"));
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, lastResponse!.StatusCode);
    }
}
