using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Genlogs.Api.Models.Dtos;
using Genlogs.Api.Tests.TestSupport;
using Xunit;

namespace Genlogs.Api.Tests.Endpoints;

public class CarrierEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public CarrierEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateAuthenticatedClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestJwtTokenFactory.CreateValidToken());
        return client;
    }

    [Fact]
    public async Task Lookup_NycToDc_ReturnsDocumentedCarriersInOrder()
    {
        var client = CreateAuthenticatedClient();

        var response = await client.PostAsJsonAsync("/api/carriers/lookup", new LookupRequest("New York City", "Washington, DC"));

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<LookupResponse>();
        Assert.NotNull(body);
        Assert.Equal(3, body!.Carriers.Count);
        Assert.Equal("Knight-Swift Transport Services", body.Carriers[0].Name);
        Assert.Equal(10, body.Carriers[0].TrucksPerDay);
        Assert.Equal("J.B. Hunt Transport Services Inc", body.Carriers[1].Name);
        Assert.Equal(7, body.Carriers[1].TrucksPerDay);
        Assert.Equal("YRC Worldwide", body.Carriers[2].Name);
        Assert.Equal(5, body.Carriers[2].TrucksPerDay);
    }

    [Fact]
    public async Task Lookup_SfToLa_ReturnsDocumentedCarriersInOrder()
    {
        var client = CreateAuthenticatedClient();

        var response = await client.PostAsJsonAsync("/api/carriers/lookup", new LookupRequest("San Francisco", "Los Angeles"));

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<LookupResponse>();
        Assert.NotNull(body);
        Assert.Equal(3, body!.Carriers.Count);
        Assert.Equal("XPO Logistics", body.Carriers[0].Name);
        Assert.Equal("Schneider", body.Carriers[1].Name);
        Assert.Equal("Landstar Systems", body.Carriers[2].Name);
    }

    [Fact]
    public async Task Lookup_UnmatchedPair_ReturnsDefaultFallbackCarriers()
    {
        var client = CreateAuthenticatedClient();

        var response = await client.PostAsJsonAsync("/api/carriers/lookup", new LookupRequest("Chicago", "Denver"));

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<LookupResponse>();
        Assert.NotNull(body);
        Assert.Equal(2, body!.Carriers.Count);
        Assert.Equal("UPS Inc.", body.Carriers[0].Name);
        Assert.Equal(11, body.Carriers[0].TrucksPerDay);
        Assert.Equal("FedEx Corp", body.Carriers[1].Name);
        Assert.Equal(9, body.Carriers[1].TrucksPerDay);
    }

    [Fact]
    public async Task Lookup_MissingOrigin_ReturnsValidationProblem()
    {
        var client = CreateAuthenticatedClient();

        var response = await client.PostAsJsonAsync("/api/carriers/lookup", new LookupRequest(null, "Washington, DC"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Origin", body);
    }

    [Fact]
    public async Task Lookup_EmptyDestination_ReturnsValidationProblem()
    {
        var client = CreateAuthenticatedClient();

        var response = await client.PostAsJsonAsync("/api/carriers/lookup", new LookupRequest("New York City", "   "));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Lookup_MalformedFieldType_ReturnsBadRequestNotServerError()
    {
        var client = CreateAuthenticatedClient();
        using var content = new StringContent("{\"origin\": 123, \"destination\": \"Washington, DC\"}", Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/carriers/lookup", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("at Genlogs.Api", body);
        Assert.DoesNotContain("StackTrace", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Lookup_NoBearerCredential_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/carriers/lookup", new LookupRequest("New York City", "Washington, DC"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Lookup_MalformedBearerCredential_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-real-jwt");

        var response = await client.PostAsJsonAsync("/api/carriers/lookup", new LookupRequest("New York City", "Washington, DC"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Lookup_ExpiredBearerCredential_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestJwtTokenFactory.CreateExpiredToken());

        var response = await client.PostAsJsonAsync("/api/carriers/lookup", new LookupRequest("New York City", "Washington, DC"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Lookup_BearerCredentialSignedWithDifferentKey_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestJwtTokenFactory.CreateTokenSignedWithDifferentKey());

        var response = await client.PostAsJsonAsync("/api/carriers/lookup", new LookupRequest("New York City", "Washington, DC"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
