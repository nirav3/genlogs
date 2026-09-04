using System.Security.Claims;
using System.Text;
using Genlogs.Api.Startup;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Genlogs.Api.Tests.TestSupport;

/// <summary>
/// Mints bearer tokens signed with the same static key Program.cs trusts only in the "Testing"
/// environment (see <see cref="TestAuthConstants"/> and design.md's "Testing implication") — the
/// replacement for the old session-token test helper now that there's no session credential to fake.
/// </summary>
public static class TestJwtTokenFactory
{
    private const string DifferentSigningKey = "a-completely-different-signing-secret-not-trusted-by-app";

    public static string CreateValidToken(TimeSpan? lifetime = null) =>
        CreateToken(TestAuthConstants.SigningKey, DateTime.UtcNow.Add(lifetime ?? TimeSpan.FromMinutes(30)));

    public static string CreateExpiredToken() =>
        CreateToken(TestAuthConstants.SigningKey, DateTime.UtcNow.AddHours(-1));

    public static string CreateTokenSignedWithDifferentKey() =>
        CreateToken(DifferentSigningKey, DateTime.UtcNow.AddMinutes(30));

    private static string CreateToken(string signingKeySecret, DateTime expires)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKeySecret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[] { new Claim("sub", "test-user") }),
            Expires = expires,
            SigningCredentials = credentials,
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}
