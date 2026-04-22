using Flowmetry.Application.Security;
using Flowmetry.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Flowmetry.Infrastructure.Security;

public class SecurityAdminService(FlowmetryDbContext db) : ISecurityAdminService
{
    public async Task<IReadOnlyList<SecurityObjectDto>> GetObjectsAsync(CancellationToken ct = default)
    {
        return await db.SecurityObjects
            .Select(o => new SecurityObjectDto(o.Id, o.Title, o.Description, o.ParentId, o.IsEnabled))
            .ToListAsync(ct);
    }

    public async Task<SecurityObjectDto> PatchObjectAsync(int id, bool isEnabled, CancellationToken ct = default)
    {
        if (id == 7)
            throw new InvalidOperationException("Cannot disable the Security object.");

        var entity = await db.SecurityObjects.FindAsync([id], ct)
            ?? throw new KeyNotFoundException($"Security object {id} not found.");

        entity.IsEnabled = isEnabled;
        await db.SaveChangesAsync(ct);

        return new SecurityObjectDto(entity.Id, entity.Title, entity.Description, entity.ParentId, entity.IsEnabled);
    }

    public async Task<IReadOnlyList<RoleWithPermissionsDto>> GetRolesAsync(CancellationToken ct = default)
    {
        var roles = await db.SecurityRoles.ToListAsync(ct);

        var permissionsByRole = await db.SecurityRolePermissions
            .GroupBy(rp => rp.RoleId)
            .Select(g => new { RoleId = g.Key, PermissionIds = g.Select(rp => rp.PermissionId).ToArray() })
            .ToListAsync(ct);

        var permMap = permissionsByRole.ToDictionary(x => x.RoleId, x => x.PermissionIds);

        return roles
            .Select(r => new RoleWithPermissionsDto(
                r.Id,
                r.RoleName,
                permMap.TryGetValue(r.Id, out var ids) ? ids : []))
            .ToList();
    }

    public async Task<IReadOnlyList<UserWithRolesDto>> GetUsersAsync(CancellationToken ct = default)
    {
        var users = await db.Users.OfType<AppUser>().ToListAsync(ct);

        var rolesByUser = await db.SecurityRoleUsers
            .GroupBy(ru => ru.UserId)
            .Select(g => new { UserId = g.Key, RoleIds = g.Select(ru => ru.RoleId).ToArray() })
            .ToListAsync(ct);

        var roleMap = rolesByUser.ToDictionary(x => x.UserId, x => x.RoleIds);

        return users
            .Select(u => new UserWithRolesDto(
                u.Id,
                u.DisplayName,
                u.Email ?? string.Empty,
                roleMap.TryGetValue(u.Id, out var ids) ? ids : []))
            .ToList();
    }

    public async Task<RoleWithPermissionsDto> AddPermissionToRoleAsync(int roleId, int permissionId, CancellationToken ct = default)
    {
        db.SecurityRolePermissions.Add(new SecurityRolePermission { RoleId = roleId, PermissionId = permissionId });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            db.ChangeTracker.Clear();
        }

        return await LoadRoleWithPermissionsAsync(roleId, ct);
    }

    public async Task<RoleWithPermissionsDto> RemovePermissionFromRoleAsync(int roleId, int permissionId, CancellationToken ct = default)
    {
        var entity = await db.SecurityRolePermissions
            .FirstOrDefaultAsync(rp => rp.RoleId == roleId && rp.PermissionId == permissionId, ct);

        if (entity is not null)
        {
            db.SecurityRolePermissions.Remove(entity);
            await db.SaveChangesAsync(ct);
        }

        return await LoadRoleWithPermissionsAsync(roleId, ct);
    }

    public async Task<UserWithRolesDto> AddRoleToUserAsync(string userId, int roleId, CancellationToken ct = default)
    {
        db.SecurityRoleUsers.Add(new SecurityRoleUser { RoleId = roleId, UserId = userId });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            db.ChangeTracker.Clear();
        }

        return await LoadUserWithRolesAsync(userId, ct);
    }

    public async Task<UserWithRolesDto> RemoveRoleFromUserAsync(string userId, int roleId, CancellationToken ct = default)
    {
        var entity = await db.SecurityRoleUsers
            .FirstOrDefaultAsync(ru => ru.UserId == userId && ru.RoleId == roleId, ct);

        if (entity is not null)
        {
            db.SecurityRoleUsers.Remove(entity);
            await db.SaveChangesAsync(ct);
        }

        return await LoadUserWithRolesAsync(userId, ct);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private async Task<RoleWithPermissionsDto> LoadRoleWithPermissionsAsync(int roleId, CancellationToken ct)
    {
        var role = await db.SecurityRoles.FindAsync([roleId], ct)
            ?? throw new KeyNotFoundException($"Role {roleId} not found.");

        var permissionIds = await db.SecurityRolePermissions
            .Where(rp => rp.RoleId == roleId)
            .Select(rp => rp.PermissionId)
            .ToArrayAsync(ct);

        return new RoleWithPermissionsDto(role.Id, role.RoleName, permissionIds);
    }

    private async Task<UserWithRolesDto> LoadUserWithRolesAsync(string userId, CancellationToken ct)
    {
        var user = await db.Users.OfType<AppUser>().FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new KeyNotFoundException($"User {userId} not found.");

        var roleIds = await db.SecurityRoleUsers
            .Where(ru => ru.UserId == userId)
            .Select(ru => ru.RoleId)
            .ToArrayAsync(ct);

        return new UserWithRolesDto(user.Id, user.DisplayName, user.Email ?? string.Empty, roleIds);
    }
}
