using Flowmetry.Infrastructure;
using Flowmetry.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Flowmetry.API.Tests.UnitTests;

public class PermissionServiceTests
{
    private static FlowmetryDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<FlowmetryDbContext>()
            .UseInMemoryDatabase($"PermissionServiceTests-{Guid.NewGuid()}")
            .Options);

    /// <summary>
    /// Requirements 3.3, 3.4:
    /// A user with no role assignments should receive an empty permissions array,
    /// and SecurityObjectStatus should contain ALL seeded security objects.
    /// </summary>
    [Fact]
    public async Task GetUserPermissionsAsync_UserWithNoRoles_ReturnsEmptyPermissionsAndAllObjects()
    {
        // Arrange
        await using var db = CreateDbContext();

        var objects = new[]
        {
            new SecurityObject { Id = 1, Title = "Flowmetry", IsEnabled = true },
            new SecurityObject { Id = 2, Title = "Dashboard", ParentId = 1, IsEnabled = true },
            new SecurityObject { Id = 3, Title = "Reports",   ParentId = 1, IsEnabled = false },
        };
        db.SecurityObjects.AddRange(objects);
        await db.SaveChangesAsync();

        var service = new PermissionService(db);

        // Act
        var result = await service.GetUserPermissionsAsync("test-user-id");

        // Assert: Requirement 3.3 — empty permissions when user has no roles
        Assert.Empty(result.Permissions);

        // Assert: Requirement 3.4 — all security objects are returned
        Assert.Equal(objects.Length, result.SecurityObjectStatus.Count);

        // Assert: is_enabled values match what was seeded
        foreach (var obj in objects)
        {
            Assert.True(result.SecurityObjectStatus.ContainsKey(obj.Id),
                $"SecurityObjectStatus should contain key {obj.Id} ({obj.Title})");
            Assert.Equal(obj.IsEnabled, result.SecurityObjectStatus[obj.Id]);
        }
    }
}
