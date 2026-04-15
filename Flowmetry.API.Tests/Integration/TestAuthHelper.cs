using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Flowmetry.API.Tests.Integration;

/// <summary>
/// Generates a valid JWT for use in integration tests.
/// Uses the same dev fallback secret as ServiceCollectionExtensions.
/// </summary>
public static class TestAuthHelper
{
    private const string Secret = "dev-only-secret-not-for-production-use-32ch";
    private const string Issuer = "flowmetry";
    private const string Audience = "flowmetry";

    public static string GenerateToken(string userId = "test-user-id", string email = "test@flowmetry.dev")
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim("displayName", "Test User"),
        };

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Creates an HttpClient with a valid Bearer token already attached.
    /// </summary>
    public static HttpClient CreateAuthenticatedClient(HttpClient client)
    {
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", GenerateToken());
        return client;
    }
}
