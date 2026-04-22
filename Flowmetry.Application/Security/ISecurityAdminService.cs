namespace Flowmetry.Application.Security;

public interface ISecurityAdminService
{
    Task<IReadOnlyList<SecurityObjectDto>> GetObjectsAsync(CancellationToken ct = default);
    Task<SecurityObjectDto> PatchObjectAsync(int id, bool isEnabled, CancellationToken ct = default);
    Task<IReadOnlyList<RoleWithPermissionsDto>> GetRolesAsync(CancellationToken ct = default);
    Task<RoleWithPermissionsDto> AddPermissionToRoleAsync(int roleId, int permissionId, CancellationToken ct = default);
    Task<RoleWithPermissionsDto> RemovePermissionFromRoleAsync(int roleId, int permissionId, CancellationToken ct = default);
    Task<IReadOnlyList<UserWithRolesDto>> GetUsersAsync(CancellationToken ct = default);
    Task<UserWithRolesDto> AddRoleToUserAsync(string userId, int roleId, CancellationToken ct = default);
    Task<UserWithRolesDto> RemoveRoleFromUserAsync(string userId, int roleId, CancellationToken ct = default);
}
