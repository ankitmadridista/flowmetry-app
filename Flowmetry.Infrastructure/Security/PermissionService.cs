using Flowmetry.Application.Security;
using Microsoft.EntityFrameworkCore;

namespace Flowmetry.Infrastructure.Security;

public class PermissionService(FlowmetryDbContext db) : IPermissionService
{
    public async Task<PermissionsDto> GetUserPermissionsAsync(string userId, CancellationToken ct = default)
    {
        // Requirement 3.4: always return all security objects regardless of user roles
        var securityObjectStatus = await db.SecurityObjects
            .ToDictionaryAsync(o => o.Id, o => o.IsEnabled, ct);

        // Requirements 3.1, 3.2: join role_users → role_permissions → permissions → objects,
        // filtered by userId and is_enabled = true
        var permissionIds = await db.SecurityRoleUsers
            .Where(ru => ru.UserId == userId)
            .Join(db.SecurityRolePermissions,
                ru => ru.RoleId,
                rp => rp.RoleId,
                (ru, rp) => rp.PermissionId)
            .Join(db.SecurityPermissions,
                permId => permId,
                p => p.Id,
                (permId, p) => p)
            .Join(db.SecurityObjects,
                p => p.ObjId,
                o => o.Id,
                (p, o) => new { Permission = p, Object = o })
            .Where(x => x.Object.IsEnabled)
            .Select(x => x.Permission.Id)
            .Distinct()
            .ToArrayAsync(ct);

        // Requirement 3.3: empty array when user has no role assignments (naturally handled above)
        return new PermissionsDto(securityObjectStatus, permissionIds);
    }
}
