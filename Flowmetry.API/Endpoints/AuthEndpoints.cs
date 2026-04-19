using Flowmetry.Application.Security;
using Flowmetry.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;

namespace Flowmetry.API.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth");

        // POST /api/auth/register
        group.MapPost("register", async (
            RegisterRequest request,
            UserManager<AppUser> userManager) =>
        {
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
                return Results.BadRequest(new { errors = new[] { "Email and password are required." } });

            var user = new AppUser
            {
                UserName = request.Email.Trim().ToLowerInvariant(),
                Email = request.Email.Trim().ToLowerInvariant(),
                DisplayName = request.DisplayName?.Trim() ?? request.Email.Split('@')[0],
            };

            var result = await userManager.CreateAsync(user, request.Password);
            if (!result.Succeeded)
            {
                var errors = result.Errors.Select(e => e.Description).ToArray();
                return Results.BadRequest(new { errors });
            }

            return Results.Created($"/api/auth/users/{user.Id}", new { id = user.Id });
        });

        // POST /api/auth/login
        group.MapPost("login", async (
            LoginRequest request,
            UserManager<AppUser> userManager,
            JwtTokenService tokenService) =>
        {
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
                return Results.BadRequest(new { errors = new[] { "Email and password are required." } });

            var user = await userManager.FindByEmailAsync(request.Email.Trim().ToLowerInvariant());
            if (user is null || !await userManager.CheckPasswordAsync(user, request.Password))
                return Results.Unauthorized();

            var token = tokenService.GenerateToken(user);

            return Results.Ok(new
            {
                token,
                user = new { id = user.Id, email = user.Email, displayName = user.DisplayName }
            });
        });

        // GET /api/auth/me — returns current user from JWT
        group.MapGet("me", (ClaimsPrincipal principal) =>
        {
            var id = principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                  ?? principal.FindFirst("sub")?.Value;
            var email = principal.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
                     ?? principal.FindFirst("email")?.Value;
            var displayName = principal.FindFirst("displayName")?.Value;

            return Results.Ok(new { id, email, displayName });
        }).RequireAuthorization();

        // GET /api/auth/permissions — returns the calling user's effective permissions
        group.MapGet("permissions", async (
            ClaimsPrincipal principal,
            IPermissionService permissionService,
            CancellationToken ct) =>
        {
            var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                      ?? principal.FindFirst("sub")?.Value
                      ?? string.Empty;

            var permissionsDto = await permissionService.GetUserPermissionsAsync(userId, ct);
            return Results.Ok(permissionsDto);
        }).RequireAuthorization();

        return app;
    }
}

public record RegisterRequest(string Email, string Password, string? DisplayName);
public record LoginRequest(string Email, string Password);
