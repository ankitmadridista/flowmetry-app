using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Flowmetry.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedSecurityObject : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
INSERT INTO security_objects (""Id"", ""Title"", ""Description"", ""ParentId"", ""IsEnabled"") SELECT 7, 'Security', NULL, 1, true WHERE NOT EXISTS (SELECT 1 FROM security_objects WHERE ""Id"" = 7);

INSERT INTO security_permissions (""Id"", ""ObjId"", ""OperationId"", ""Title"") SELECT 21, 7, 1, 'Security View'   WHERE NOT EXISTS (SELECT 1 FROM security_permissions WHERE ""Id"" = 21);
INSERT INTO security_permissions (""Id"", ""ObjId"", ""OperationId"", ""Title"") SELECT 22, 7, 3, 'Security Edit'   WHERE NOT EXISTS (SELECT 1 FROM security_permissions WHERE ""Id"" = 22);
INSERT INTO security_permissions (""Id"", ""ObjId"", ""OperationId"", ""Title"") SELECT 23, 7, 6, 'Security Manage' WHERE NOT EXISTS (SELECT 1 FROM security_permissions WHERE ""Id"" = 23);

INSERT INTO security_role_permissions (""RoleId"", ""PermissionId"")
SELECT 1, p.id FROM (VALUES (21),(22),(23)) AS p(id)
WHERE NOT EXISTS (SELECT 1 FROM security_role_permissions WHERE ""RoleId"" = 1 AND ""PermissionId"" = p.id);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DELETE FROM security_role_permissions WHERE ""RoleId"" = 1 AND ""PermissionId"" IN (21, 22, 23);
DELETE FROM security_permissions WHERE ""Id"" IN (21, 22, 23);
DELETE FROM security_objects WHERE ""Id"" = 7;
");
        }
    }
}
