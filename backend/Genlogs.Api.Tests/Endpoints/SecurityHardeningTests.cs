using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Genlogs.Api.Models.Dtos;
using Genlogs.Api.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc.Testing;
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

public class ForwardedHeadersTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ForwardedHeadersTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task RequestWithForwardedHttpsProto_IsNotRedirected()
    {
        // Simulates Render's edge: client connected over https, Render's internal hop to the
        // container is plain http, with X-Forwarded-Proto carrying the original scheme. Without
        // ForwardedHeaders middleware, UseHttpsRedirection() would see "http" here and issue a
        // redirect back to https — which the client already used, producing a loop.
        //
        // HTTPS_PORT is set explicitly so UseHttpsRedirection() can actually resolve a redirect
        // target in-process (TestServer has no real HTTPS endpoint to infer one from) — without
        // this, the middleware silently no-ops on any non-https request regardless of whether
        // ForwardedHeaders is wired up, which would make this test pass even with the bug present.
        using var innerFactory = _factory.WithWebHostBuilder(builder => builder.UseSetting("HTTPS_PORT", "443"));
        using var client = innerFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("X-Forwarded-Proto", "https");
        request.Headers.Add("X-Forwarded-For", "203.0.113.10");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
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
