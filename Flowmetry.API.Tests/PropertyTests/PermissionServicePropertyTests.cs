// Feature: rbac-permissions, Property 3: Permission union across roles
// Feature: rbac-permissions, Property 4: Disabled object filtering
// Feature: rbac-permissions, Property 5: All objects always returned

using CsCheck;
using Flowmetry.Infrastructure;
using Flowmetry.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Flowmetry.API.Tests.PropertyTests;

public class PermissionServicePropertyTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static FlowmetryDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<FlowmetryDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new FlowmetryDbContext(options);
    }

    // ── Property 3: Permission union across roles ─────────────────────────────

    [Fact]
    public async Task Property3_PermissionUnionAcrossRoles()
    {
        // Feature: rbac-permissions, Property 3: Permission union across roles
        // **Validates: Requirements 3.1**
        //
        // For any user assigned to an arbitrary set of roles, the permission IDs
        // returned by GetUserPermissionsAsync should equal the set-union of all
        // permission IDs granted to each of those roles (restricted to enabled objects).

        var gen =
            from roleCount in Gen.Int[1, 5]
            from permsPerRole in Gen.Int[1, 4].Array[roleCount]
            select (roleCount, permsPerRole);

        await gen.SampleAsync(async tuple =>
        {
            await using var ctx = CreateContext();

            var (roleCount, permsPerRole) = tuple;

            // Create one SecurityOperation
            var operation = new SecurityOperation { Id = 1, OperationType = "View" };
            ctx.SecurityOperations.Add(operation);

            // Create SecurityObjects (all enabled) — one per role for simplicity
            var objects = Enumerable.Range(1, roleCount).Select(i => new SecurityObject
            {
                Id = i,
                Title = $"Object{i}",
                IsEnabled = true
            }).ToList();
            ctx.SecurityObjects.AddRange(objects);

            // Create SecurityPermissions — permsPerRole[i] permissions per role's object
            var allPermissions = new List<SecurityPermission>();
            int permId = 1;
            for (int r = 0; r < roleCount; r++)
            {
                int count = permsPerRole[r];
                for (int p = 0; p < count; p++)
                {
                    allPermissions.Add(new SecurityPermission
                    {
                        Id = permId++,
                        ObjId = objects[r].Id,
                        OperationId = operation.Id,
                        Title = $"Perm{permId}"
                    });
                }
            }
            ctx.SecurityPermissions.AddRange(allPermissions);

            // Create SecurityRoles
            var roles = Enumerable.Range(1, roleCount).Select(i => new SecurityRole
            {
                Id = i,
                RoleName = $"Role{i}"
            }).ToList();
            ctx.SecurityRoles.AddRange(roles);

            // Assign permissions to roles: role i gets the permissions for object i
            var rolePermissions = new List<SecurityRolePermission>();
            int pidx = 0;
            for (int r = 0; r < roleCount; r++)
            {
                int count = permsPerRole[r];
                for (int p = 0; p < count; p++)
                {
                    rolePermissions.Add(new SecurityRolePermission
                    {
                        RoleId = roles[r].Id,
                        PermissionId = allPermissions[pidx++].Id
                    });
                }
            }
            ctx.SecurityRolePermissions.AddRange(rolePermissions);

            // Assign all roles to the test user
            ctx.SecurityRoleUsers.AddRange(roles.Select(r => new SecurityRoleUser
            {
                RoleId = r.Id,
                UserId = "test-user"
            }));

            await ctx.SaveChangesAsync();

            var service = new PermissionService(ctx);
            var result = await service.GetUserPermissionsAsync("test-user");

            // Expected: union of all permission IDs (all objects are enabled)
            var expected = new HashSet<int>(allPermissions.Select(p => p.Id));
            var actual = new HashSet<int>(result.Permissions);

            Assert.Equal(expected, actual);
        }, iter: 100);
    }

    // ── Property 4: Disabled object filtering ────────────────────────────────

    [Fact]
    public async Task Property4_DisabledObjectFiltering()
    {
        // Feature: rbac-permissions, Property 4: Disabled object filtering
        // **Validates: Requirements 3.2, 4.4**
        //
        // For any security object with is_enabled = false, no permission associated
        // with that object should appear in the Permissions array.

        var gen =
            from objCount in Gen.Int[1, 6]
            from enabledFlags in Gen.Bool.Array[objCount]
            select (objCount, enabledFlags);

        await gen.SampleAsync(async tuple =>
        {
            await using var ctx = CreateContext();

            var (objCount, enabledFlags) = tuple;

            var operation = new SecurityOperation { Id = 1, OperationType = "View" };
            ctx.SecurityOperations.Add(operation);

            // Create SecurityObjects with random enabled states
            var objects = Enumerable.Range(1, objCount).Select(i => new SecurityObject
            {
                Id = i,
                Title = $"Object{i}",
                IsEnabled = enabledFlags[i - 1]
            }).ToList();
            ctx.SecurityObjects.AddRange(objects);

            // One permission per object
            var permissions = objects.Select((o, idx) => new SecurityPermission
            {
                Id = idx + 1,
                ObjId = o.Id,
                OperationId = operation.Id,
                Title = $"Perm{idx + 1}"
            }).ToList();
            ctx.SecurityPermissions.AddRange(permissions);

            // One role with all permissions
            var role = new SecurityRole { Id = 1, RoleName = "TestRole" };
            ctx.SecurityRoles.Add(role);

            ctx.SecurityRolePermissions.AddRange(permissions.Select(p => new SecurityRolePermission
            {
                RoleId = role.Id,
                PermissionId = p.Id
            }));

            ctx.SecurityRoleUsers.Add(new SecurityRoleUser { RoleId = role.Id, UserId = "test-user" });

            await ctx.SaveChangesAsync();

            var service = new PermissionService(ctx);
            var result = await service.GetUserPermissionsAsync("test-user");

            // Build a map from permissionId → object's IsEnabled
            var permToEnabled = permissions.ToDictionary(p => p.Id, p => objects.First(o => o.Id == p.ObjId).IsEnabled);

            // Assert: no returned permission belongs to a disabled object
            foreach (var permId in result.Permissions)
            {
                Assert.True(permToEnabled.ContainsKey(permId), $"Unknown permissionId {permId} in result");
                Assert.True(permToEnabled[permId], $"Permission {permId} belongs to a disabled object but was returned");
            }
        }, iter: 100);
    }

    // ── Property 5: All objects always returned ───────────────────────────────

    [Fact]
    public async Task Property5_AllObjectsAlwaysReturned()
    {
        // Feature: rbac-permissions, Property 5: All objects always returned
        // **Validates: Requirements 3.4**
        //
        // For any user (including one with no role assignments), SecurityObjectStatus
        // should contain an entry for every row in the security_objects table.

        var gen =
            from objCount in Gen.Int[1, 10]
            from hasRoles in Gen.Bool
            select (objCount, hasRoles);

        await gen.SampleAsync(async tuple =>
        {
            await using var ctx = CreateContext();

            var (objCount, hasRoles) = tuple;

            // Create SecurityObjects
            var objects = Enumerable.Range(1, objCount).Select(i => new SecurityObject
            {
                Id = i,
                Title = $"Object{i}",
                IsEnabled = true
            }).ToList();
            ctx.SecurityObjects.AddRange(objects);

            if (hasRoles)
            {
                var operation = new SecurityOperation { Id = 1, OperationType = "View" };
                ctx.SecurityOperations.Add(operation);

                var permission = new SecurityPermission
                {
                    Id = 1,
                    ObjId = objects[0].Id,
                    OperationId = operation.Id,
                    Title = "Perm1"
                };
                ctx.SecurityPermissions.Add(permission);

                var role = new SecurityRole { Id = 1, RoleName = "TestRole" };
                ctx.SecurityRoles.Add(role);

                ctx.SecurityRolePermissions.Add(new SecurityRolePermission
                {
                    RoleId = role.Id,
                    PermissionId = permission.Id
                });

                ctx.SecurityRoleUsers.Add(new SecurityRoleUser { RoleId = role.Id, UserId = "test-user" });
            }

            await ctx.SaveChangesAsync();

            var service = new PermissionService(ctx);
            var result = await service.GetUserPermissionsAsync("test-user");

            // Assert: SecurityObjectStatus contains ALL seeded objects
            Assert.Equal(objCount, result.SecurityObjectStatus.Count);

            foreach (var obj in objects)
            {
                Assert.True(result.SecurityObjectStatus.ContainsKey(obj.Id),
                    $"SecurityObjectStatus missing key for object id={obj.Id}");
            }
        }, iter: 100);
    }
}
