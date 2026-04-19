using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Flowmetry.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRbacSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "security_objects",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    ParentId = table.Column<int>(type: "integer", nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_security_objects", x => x.Id);
                    table.ForeignKey("FK_security_objects_security_objects_ParentId", x => x.ParentId, "security_objects", "Id");
                });

            migrationBuilder.CreateTable(
                name: "security_operations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OperationType = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_security_operations", x => x.Id));

            migrationBuilder.CreateTable(
                name: "security_roles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoleName = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_security_roles", x => x.Id));

            migrationBuilder.CreateTable(
                name: "security_permissions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ObjId = table.Column<int>(type: "integer", nullable: false),
                    OperationId = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_security_permissions", x => x.Id);
                    table.ForeignKey("FK_security_permissions_security_objects_ObjId", x => x.ObjId, "security_objects", "Id", onDelete: ReferentialAction.Cascade);
                    table.ForeignKey("FK_security_permissions_security_operations_OperationId", x => x.OperationId, "security_operations", "Id", onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "security_role_permissions",
                columns: table => new
                {
                    RoleId = table.Column<int>(type: "integer", nullable: false),
                    PermissionId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_security_role_permissions", x => new { x.RoleId, x.PermissionId });
                    table.ForeignKey("FK_security_role_permissions_security_permissions_PermissionId", x => x.PermissionId, "security_permissions", "Id", onDelete: ReferentialAction.Cascade);
                    table.ForeignKey("FK_security_role_permissions_security_roles_RoleId", x => x.RoleId, "security_roles", "Id", onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "security_role_users",
                columns: table => new
                {
                    RoleId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_security_role_users", x => new { x.RoleId, x.UserId });
                    table.ForeignKey("FK_security_role_users_AspNetUsers_UserId", x => x.UserId, "AspNetUsers", "Id", onDelete: ReferentialAction.Cascade);
                    table.ForeignKey("FK_security_role_users_security_roles_RoleId", x => x.RoleId, "security_roles", "Id", onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex("IX_security_objects_ParentId", "security_objects", "ParentId");
            migrationBuilder.CreateIndex("IX_security_permissions_ObjId", "security_permissions", "ObjId");
            migrationBuilder.CreateIndex("IX_security_permissions_OperationId", "security_permissions", "OperationId");
            migrationBuilder.CreateIndex("IX_security_role_permissions_PermissionId", "security_role_permissions", "PermissionId");
            migrationBuilder.CreateIndex("IX_security_role_users_UserId", "security_role_users", "UserId");

            migrationBuilder.Sql(@"
INSERT INTO security_objects (""Id"", ""Title"", ""Description"", ""ParentId"", ""IsEnabled"") SELECT 1, 'Flowmetry', NULL, NULL, true WHERE NOT EXISTS (SELECT 1 FROM security_objects WHERE ""Id"" = 1);
INSERT INTO security_objects (""Id"", ""Title"", ""Description"", ""ParentId"", ""IsEnabled"") SELECT 2, 'Dashboard', NULL, 1, true WHERE NOT EXISTS (SELECT 1 FROM security_objects WHERE ""Id"" = 2);
INSERT INTO security_objects (""Id"", ""Title"", ""Description"", ""ParentId"", ""IsEnabled"") SELECT 3, 'Invoices', NULL, 1, true WHERE NOT EXISTS (SELECT 1 FROM security_objects WHERE ""Id"" = 3);
INSERT INTO security_objects (""Id"", ""Title"", ""Description"", ""ParentId"", ""IsEnabled"") SELECT 4, 'Customers', NULL, 1, true WHERE NOT EXISTS (SELECT 1 FROM security_objects WHERE ""Id"" = 4);
INSERT INTO security_objects (""Id"", ""Title"", ""Description"", ""ParentId"", ""IsEnabled"") SELECT 5, 'Payments', NULL, 3, true WHERE NOT EXISTS (SELECT 1 FROM security_objects WHERE ""Id"" = 5);
INSERT INTO security_objects (""Id"", ""Title"", ""Description"", ""ParentId"", ""IsEnabled"") SELECT 6, 'Reports', NULL, 1, true WHERE NOT EXISTS (SELECT 1 FROM security_objects WHERE ""Id"" = 6);

INSERT INTO security_operations (""Id"", ""OperationType"") SELECT 1, 'View'   WHERE NOT EXISTS (SELECT 1 FROM security_operations WHERE ""Id"" = 1);
INSERT INTO security_operations (""Id"", ""OperationType"") SELECT 2, 'Create' WHERE NOT EXISTS (SELECT 1 FROM security_operations WHERE ""Id"" = 2);
INSERT INTO security_operations (""Id"", ""OperationType"") SELECT 3, 'Edit'   WHERE NOT EXISTS (SELECT 1 FROM security_operations WHERE ""Id"" = 3);
INSERT INTO security_operations (""Id"", ""OperationType"") SELECT 4, 'Delete' WHERE NOT EXISTS (SELECT 1 FROM security_operations WHERE ""Id"" = 4);
INSERT INTO security_operations (""Id"", ""OperationType"") SELECT 5, 'Copy'   WHERE NOT EXISTS (SELECT 1 FROM security_operations WHERE ""Id"" = 5);
INSERT INTO security_operations (""Id"", ""OperationType"") SELECT 6, 'Manage' WHERE NOT EXISTS (SELECT 1 FROM security_operations WHERE ""Id"" = 6);

INSERT INTO security_roles (""Id"", ""RoleName"") SELECT 1, 'Admin'   WHERE NOT EXISTS (SELECT 1 FROM security_roles WHERE ""Id"" = 1);
INSERT INTO security_roles (""Id"", ""RoleName"") SELECT 2, 'Manager' WHERE NOT EXISTS (SELECT 1 FROM security_roles WHERE ""Id"" = 2);
INSERT INTO security_roles (""Id"", ""RoleName"") SELECT 3, 'Viewer'  WHERE NOT EXISTS (SELECT 1 FROM security_roles WHERE ""Id"" = 3);

INSERT INTO security_permissions (""Id"", ""ObjId"", ""OperationId"", ""Title"") SELECT  1, 2, 1, 'Dashboard View'   WHERE NOT EXISTS (SELECT 1 FROM security_permissions WHERE ""Id"" =  1);
INSERT INTO security_permissions (""Id"", ""ObjId"", ""OperationId"", ""Title"") SELECT  2, 2, 2, 'Dashboard Create' WHERE NOT EXISTS (SELECT 1 FROM security_permissions WHERE ""Id"" =  2);
INSERT INTO security_permissions (""Id"", ""ObjId"", ""OperationId"", ""Title"") SELECT  3, 2, 3, 'Dashboard Edit'   WHERE NOT EXISTS (SELECT 1 FROM security_permissions WHERE ""Id"" =  3);
INSERT INTO security_permissions (""Id"", ""ObjId"", ""OperationId"", ""Title"") SELECT  4, 2, 4, 'Dashboard Delete' WHERE NOT EXISTS (SELECT 1 FROM security_permissions WHERE ""Id"" =  4);
INSERT INTO security_permissions (""Id"", ""ObjId"", ""OperationId"", ""Title"") SELECT  5, 3, 1, 'Invoices View'    WHERE NOT EXISTS (SELECT 1 FROM security_permissions WHERE ""Id"" =  5);
INSERT INTO security_permissions (""Id"", ""ObjId"", ""OperationId"", ""Title"") SELECT  6, 3, 2, 'Invoices Create'  WHERE NOT EXISTS (SELECT 1 FROM security_permissions WHERE ""Id"" =  6);
INSERT INTO security_permissions (""Id"", ""ObjId"", ""OperationId"", ""Title"") SELECT  7, 3, 3, 'Invoices Edit'    WHERE NOT EXISTS (SELECT 1 FROM security_permissions WHERE ""Id"" =  7);
INSERT INTO security_permissions (""Id"", ""ObjId"", ""OperationId"", ""Title"") SELECT  8, 3, 4, 'Invoices Delete'  WHERE NOT EXISTS (SELECT 1 FROM security_permissions WHERE ""Id"" =  8);
INSERT INTO security_permissions (""Id"", ""ObjId"", ""OperationId"", ""Title"") SELECT  9, 4, 1, 'Customers View'   WHERE NOT EXISTS (SELECT 1 FROM security_permissions WHERE ""Id"" =  9);
INSERT INTO security_permissions (""Id"", ""ObjId"", ""OperationId"", ""Title"") SELECT 10, 4, 2, 'Customers Create' WHERE NOT EXISTS (SELECT 1 FROM security_permissions WHERE ""Id"" = 10);
INSERT INTO security_permissions (""Id"", ""ObjId"", ""OperationId"", ""Title"") SELECT 11, 4, 3, 'Customers Edit'   WHERE NOT EXISTS (SELECT 1 FROM security_permissions WHERE ""Id"" = 11);
INSERT INTO security_permissions (""Id"", ""ObjId"", ""OperationId"", ""Title"") SELECT 12, 4, 4, 'Customers Delete' WHERE NOT EXISTS (SELECT 1 FROM security_permissions WHERE ""Id"" = 12);
INSERT INTO security_permissions (""Id"", ""ObjId"", ""OperationId"", ""Title"") SELECT 13, 5, 1, 'Payments View'    WHERE NOT EXISTS (SELECT 1 FROM security_permissions WHERE ""Id"" = 13);
INSERT INTO security_permissions (""Id"", ""ObjId"", ""OperationId"", ""Title"") SELECT 14, 5, 2, 'Payments Create'  WHERE NOT EXISTS (SELECT 1 FROM security_permissions WHERE ""Id"" = 14);
INSERT INTO security_permissions (""Id"", ""ObjId"", ""OperationId"", ""Title"") SELECT 15, 5, 3, 'Payments Edit'    WHERE NOT EXISTS (SELECT 1 FROM security_permissions WHERE ""Id"" = 15);
INSERT INTO security_permissions (""Id"", ""ObjId"", ""OperationId"", ""Title"") SELECT 16, 5, 4, 'Payments Delete'  WHERE NOT EXISTS (SELECT 1 FROM security_permissions WHERE ""Id"" = 16);
INSERT INTO security_permissions (""Id"", ""ObjId"", ""OperationId"", ""Title"") SELECT 17, 6, 1, 'Reports View'     WHERE NOT EXISTS (SELECT 1 FROM security_permissions WHERE ""Id"" = 17);
INSERT INTO security_permissions (""Id"", ""ObjId"", ""OperationId"", ""Title"") SELECT 18, 6, 2, 'Reports Create'   WHERE NOT EXISTS (SELECT 1 FROM security_permissions WHERE ""Id"" = 18);
INSERT INTO security_permissions (""Id"", ""ObjId"", ""OperationId"", ""Title"") SELECT 19, 6, 3, 'Reports Edit'     WHERE NOT EXISTS (SELECT 1 FROM security_permissions WHERE ""Id"" = 19);
INSERT INTO security_permissions (""Id"", ""ObjId"", ""OperationId"", ""Title"") SELECT 20, 6, 4, 'Reports Delete'   WHERE NOT EXISTS (SELECT 1 FROM security_permissions WHERE ""Id"" = 20);

INSERT INTO security_role_permissions (""RoleId"", ""PermissionId"")
SELECT 1, p.id FROM (VALUES (1),(2),(3),(4),(5),(6),(7),(8),(9),(10),(11),(12),(13),(14),(15),(16),(17),(18),(19),(20)) AS p(id)
WHERE NOT EXISTS (SELECT 1 FROM security_role_permissions WHERE ""RoleId"" = 1 AND ""PermissionId"" = p.id);

INSERT INTO security_role_permissions (""RoleId"", ""PermissionId"")
SELECT 2, p.id FROM (VALUES (1),(2),(3),(5),(6),(7),(9),(10),(11),(13),(14),(15),(17),(18),(19)) AS p(id)
WHERE NOT EXISTS (SELECT 1 FROM security_role_permissions WHERE ""RoleId"" = 2 AND ""PermissionId"" = p.id);

INSERT INTO security_role_permissions (""RoleId"", ""PermissionId"")
SELECT 3, p.id FROM (VALUES (1),(5),(9),(13),(17)) AS p(id)
WHERE NOT EXISTS (SELECT 1 FROM security_role_permissions WHERE ""RoleId"" = 3 AND ""PermissionId"" = p.id);
");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "security_role_users");
            migrationBuilder.DropTable(name: "security_role_permissions");
            migrationBuilder.DropTable(name: "security_permissions");
            migrationBuilder.DropTable(name: "security_roles");
            migrationBuilder.DropTable(name: "security_operations");
            migrationBuilder.DropTable(name: "security_objects");
        }
    }
}
