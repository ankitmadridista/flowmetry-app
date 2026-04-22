using System.Security.Claims;
using Flowmetry.Application.Security;

namespace Flowmetry.API.Endpoints;

public static class SecurityAdminEndpoints
{
    public static IEndpointRouteBuilder MapSecurityAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/security-admin").RequireAuthorization();

        // GET /api/security-admin/objects
        group.MapGet("objects", async (ClaimsPrincipal user, IPermissionService ps, ISecurityAdminService svc, CancellationToken ct) =>
        {
            var ids = await GetUserPermissionIds(user, ps, ct);
            if (!CanView(ids)) return Results.Forbid();
            var result = await svc.GetObjectsAsync(ct);
            return Results.Ok(result);
        });

        // PATCH /api/security-admin/objects/{id}
        group.MapMethods("objects/{id:int}", ["PATCH"], async (int id, PatchObjectRequest body, ClaimsPrincipal user, IPermissionService ps, ISecurityAdminService svc, CancellationToken ct) =>
        {
            var ids = await GetUserPermissionIds(user, ps, ct);
            if (!CanMutate(ids)) return Results.Forbid();
            try
            {
                var result = await svc.PatchObjectAsync(id, body.IsEnabled, ct);
                return Results.Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return Results.UnprocessableEntity(new { message = ex.Message });
            }
        });

        // GET /api/security-admin/roles
        group.MapGet("roles", async (ClaimsPrincipal user, IPermissionService ps, ISecurityAdminService svc, CancellationToken ct) =>
        {
            var ids = await GetUserPermissionIds(user, ps, ct);
            if (!CanView(ids)) return Results.Forbid();
            var result = await svc.GetRolesAsync(ct);
            return Results.Ok(result);
        });

        // POST /api/security-admin/roles/{roleId}/permissions
        group.MapPost("roles/{roleId:int}/permissions", async (int roleId, AddPermissionRequest body, ClaimsPrincipal user, IPermissionService ps, ISecurityAdminService svc, CancellationToken ct) =>
        {
            var ids = await GetUserPermissionIds(user, ps, ct);
            if (!CanMutate(ids)) return Results.Forbid();
            var result = await svc.AddPermissionToRoleAsync(roleId, body.PermissionId, ct);
            return Results.Ok(result);
        });

        // DELETE /api/security-admin/roles/{roleId}/permissions/{permissionId}
        group.MapDelete("roles/{roleId:int}/permissions/{permissionId:int}", async (int roleId, int permissionId, ClaimsPrincipal user, IPermissionService ps, ISecurityAdminService svc, CancellationToken ct) =>
        {
            var ids = await GetUserPermissionIds(user, ps, ct);
            if (!CanMutate(ids)) return Results.Forbid();
            var result = await svc.RemovePermissionFromRoleAsync(roleId, permissionId, ct);
            return Results.Ok(result);
        });

        // GET /api/security-admin/users
        group.MapGet("users", async (ClaimsPrincipal user, IPermissionService ps, ISecurityAdminService svc, CancellationToken ct) =>
        {
            var ids = await GetUserPermissionIds(user, ps, ct);
            if (!CanView(ids)) return Results.Forbid();
            var result = await svc.GetUsersAsync(ct);
            return Results.Ok(result);
        });

        // POST /api/security-admin/users/{userId}/roles
        group.MapPost("users/{userId}/roles", async (string userId, AddRoleRequest body, ClaimsPrincipal user, IPermissionService ps, ISecurityAdminService svc, CancellationToken ct) =>
        {
            var ids = await GetUserPermissionIds(user, ps, ct);
            if (!CanMutate(ids)) return Results.Forbid();
            var result = await svc.AddRoleToUserAsync(userId, body.RoleId, ct);
            return Results.Ok(result);
        });

        // DELETE /api/security-admin/users/{userId}/roles/{roleId}
        group.MapDelete("users/{userId}/roles/{roleId:int}", async (string userId, int roleId, ClaimsPrincipal user, IPermissionService ps, ISecurityAdminService svc, CancellationToken ct) =>
        {
            var ids = await GetUserPermissionIds(user, ps, ct);
            if (!CanMutate(ids)) return Results.Forbid();
            var result = await svc.RemoveRoleFromUserAsync(userId, roleId, ct);
            return Results.Ok(result);
        });

        return app;
    }

    static async Task<int[]> GetUserPermissionIds(ClaimsPrincipal principal, IPermissionService ps, CancellationToken ct)
    {
        var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var dto = await ps.GetUserPermissionsAsync(userId, ct);
        return dto.Permissions;
    }

    static bool CanView(int[] ids) => ids.Contains(21) || ids.Contains(22) || ids.Contains(23);
    static bool CanMutate(int[] ids) => ids.Contains(22) || ids.Contains(23);
}
