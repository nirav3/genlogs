using System.Text;
using FluentValidation;
using Genlogs.Api.Data;
using Genlogs.Api.Endpoints;
using Genlogs.Api.Middleware;
using Genlogs.Api.Services;
using Genlogs.Api.Startup;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using NetEscapades.AspNetCore.SecurityHeaders;

var builder = WebApplication.CreateBuilder(args);

// Fail fast: an app that silently accepted unverifiable Google tokens would be a worse failure mode than
// refusing to start (design.md Risks/Trade-offs).
ConfigurationGuard.EnsureRequiredConfiguration(builder.Configuration);
var googleClientId = builder.Configuration["GOOGLE_CLIENT_ID"]!;
var isTestingEnvironment = builder.Environment.EnvironmentName == "Testing";

var allowedOrigins = (builder.Configuration["ALLOWED_ORIGIN"] ?? string.Empty)
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<GenlogsDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default") ?? "Data Source=genlogs.db"));

builder.Services.AddScoped<ILaneResolutionService, LaneResolutionService>();
builder.Services.AddScoped<ICarrierLookupService, CarrierLookupService>();

builder.Services.AddValidatorsFromAssemblyContaining<Program>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        if (isTestingEnvironment)
        {
            // No DI seam for Authority-based validation and no way to fabricate a real Google-signed
            // token in tests (design.md "Testing implication") — use a static symmetric key instead.
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestAuthConstants.SigningKey)),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero,
            };
        }
        else
        {
            options.Authority = "https://accounts.google.com";
            options.Audience = googleClientId;
        }
    });
builder.Services.AddAuthorization();

builder.Services.AddCors(options =>
{
    options.AddPolicy("Default", policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
        }
    });
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddFixedWindowLimiter(RateLimiterPolicies.Lookup, lookupOptions =>
    {
        lookupOptions.PermitLimit = 30;
        lookupOptions.Window = TimeSpan.FromMinutes(1);
        lookupOptions.QueueLimit = 0;
    });
});

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<GenlogsDbContext>();
    db.Database.Migrate();
    DbInitializer.Seed(db);
}

// Render terminates TLS at its edge and forwards plain HTTP to this container over an internal
// hop, identifying the original scheme via X-Forwarded-Proto. Must run before UseExceptionHandler
// and everything downstream (security headers, HTTPS redirection, CORS, auth, rate limiting) so
// they all see the real scheme/client IP instead of the internal http hop — otherwise
// UseHttpsRedirection() below sees "http" on every request and redirect-loops against a client
// that already connected over https. KnownNetworks/KnownProxies are cleared (not left at their
// loopback-only default) because Render's proxy isn't a fixed, known on-prem address like a
// traditional reverse proxy; this is standard guidance for PaaS platforms (Render/Heroku/Azure App
// Service) in front of ASP.NET Core, and is safe here because Render's edge is the only thing that
// can reach this container — it isn't exposed to arbitrary untrusted proxies.
var forwardedHeaderOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
};
forwardedHeaderOptions.KnownNetworks.Clear();
forwardedHeaderOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedHeaderOptions);

app.UseExceptionHandler();

var securityHeaderPolicies = new HeaderPolicyCollection().AddDefaultSecurityHeaders();
app.UseSecurityHeaders(securityHeaderPolicies);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors("Default");

app.UseAuthentication();
app.UseAuthorization();

app.UseRateLimiter();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.MapCarrierEndpoints();

app.Run();

public partial class Program
{
}
