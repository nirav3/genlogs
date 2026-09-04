using Genlogs.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Genlogs.Api.Tests.TestSupport;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string GoogleClientId = "integration-test-google-client-id";
    public const string AllowedOrigin = "https://allowed.example.com";

    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    static CustomWebApplicationFactory()
    {
        // Config Program.cs reads directly from environment variables via the standard ASP.NET Core
        // environment-variable configuration provider — set before any host is built (see report:
        // this is more reliable than WebApplicationFactory's own config hooks for pre-Build() checks).
        Environment.SetEnvironmentVariable("GOOGLE_CLIENT_ID", GoogleClientId);
        Environment.SetEnvironmentVariable("ALLOWED_ORIGIN", AllowedOrigin);
    }

    public CustomWebApplicationFactory()
    {
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Triggers Program.cs's static-signing-key branch instead of the real Authority/Audience path
        // (design.md "Testing implication") — TestJwtTokenFactory mints tokens against that same key.
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            var dbContextOptionsDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<GenlogsDbContext>));
            if (dbContextOptionsDescriptor is not null)
            {
                services.Remove(dbContextOptionsDescriptor);
            }

            services.AddDbContext<GenlogsDbContext>(options => options.UseSqlite(_connection));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection.Dispose();
        }
    }
}
