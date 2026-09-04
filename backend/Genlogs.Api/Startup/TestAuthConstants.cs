namespace Genlogs.Api.Startup;

/// <summary>
/// Static symmetric signing key used only when <c>EnvironmentName == "Testing"</c> (see Program.cs and
/// design.md's "Testing implication"). Authority-based JWT validation has no DI seam to fake in tests and
/// a real Google-signed token can't be fabricated without Google's private key, so tests need a real key
/// they can sign with themselves. Internal + `InternalsVisibleTo` (Genlogs.Api.csproj) keeps this out of
/// any public production surface while still being one shared source of truth with the test project.
/// </summary>
internal static class TestAuthConstants
{
    public const string SigningKey = "genlogs-testing-only-signing-key-never-used-outside-tests-8f2a";
}
