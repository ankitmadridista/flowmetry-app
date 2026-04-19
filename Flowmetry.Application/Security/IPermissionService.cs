namespace Flowmetry.Application.Security;

public interface IPermissionService
{
    Task<PermissionsDto> GetUserPermissionsAsync(string userId, CancellationToken ct = default);
}
