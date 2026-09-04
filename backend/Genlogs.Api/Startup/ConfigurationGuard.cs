namespace Genlogs.Api.Startup;

/// <summary>
/// Fail-fast startup checks, extracted from Program.cs so they're unit-testable in isolation from the
/// full host (design.md Risks: "GOOGLE_CLIENT_ID misconfiguration ... covered by tests asserting the app
/// fails fast at startup").
/// </summary>
public static class ConfigurationGuard
{
    public static void EnsureRequiredConfiguration(IConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(configuration["GOOGLE_CLIENT_ID"]))
        {
            throw new InvalidOperationException("GOOGLE_CLIENT_ID configuration is required but was not provided.");
        }
    }
}
