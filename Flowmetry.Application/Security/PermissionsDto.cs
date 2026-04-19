namespace Flowmetry.Application.Security;

public record PermissionsDto(
    Dictionary<int, bool> SecurityObjectStatus,
    int[] Permissions);
