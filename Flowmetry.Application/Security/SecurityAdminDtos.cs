namespace Flowmetry.Application.Security;

public record SecurityObjectDto(int Id, string Title, string? Description, int? ParentId, bool IsEnabled);
public record RoleWithPermissionsDto(int Id, string RoleName, int[] PermissionIds);
public record UserWithRolesDto(string UserId, string DisplayName, string Email, int[] RoleIds);
public record PatchObjectRequest(bool IsEnabled);
public record AddPermissionRequest(int PermissionId);
public record AddRoleRequest(int RoleId);
