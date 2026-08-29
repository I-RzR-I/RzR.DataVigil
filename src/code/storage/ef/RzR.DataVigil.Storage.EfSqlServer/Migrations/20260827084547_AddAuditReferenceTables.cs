using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace RzR.DataVigil.Storage.EfSqlServer.Migrations
{
    public partial class AddAuditReferenceTables : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RefAuditActions",
                schema: "audit",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefAuditActions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RefAuditUserSources",
                schema: "audit",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefAuditUserSources", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RefGdprFieldActions",
                schema: "audit",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefGdprFieldActions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RefGdprStorageStates",
                schema: "audit",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefGdprStorageStates", x => x.Id);
                });

            migrationBuilder.InsertData(
                schema: "audit",
                table: "RefAuditActions",
                columns: new[] { "Id", "Description", "Name" },
                values: new object[,]
                {
                    { 1, "A new entity was inserted.", "Create" },
                    { 2, "An existing entity was read.", "Read" },
                    { 3, "An existing entity was modified.", "Update" },
                    { 4, "An existing entity was deleted.", "Delete" }
                });

            migrationBuilder.InsertData(
                schema: "audit",
                table: "RefAuditUserSources",
                columns: new[] { "Id", "Description", "Name" },
                values: new object[,]
                {
                    { 4, "User extracted from the ASP.NET Core HttpContext authenticated principal.", "HttpContext" },
                    { 3, "User taken from an IAuditScopeContext manual override.", "ScopeContext" },
                    { 5, "User extracted from Thread.CurrentPrincipal.", "ThreadPrincipal" },
                    { 1, "No resolver could be consulted or resolution failed; attribution is unknown.", "Unresolved" },
                    { 0, "Resolver returned a user but did not declare where that identity came from. The recorded actor is real and can be trusted; only its provenance is undeclared. Contrast with Unresolved, where attribution itself failed.", "Unspecified" },
                    { 2, "Resolution succeeded and there is genuinely no user for this action.", "Anonymous" }
                });

            migrationBuilder.InsertData(
                schema: "audit",
                table: "RefGdprFieldActions",
                columns: new[] { "Id", "Description", "Name" },
                values: new object[,]
                {
                    { 0, "Field is not stored or displayed at all.", "Exclude" },
                    { 1, "Field is partially hidden, for example j***@mail.com.", "Mask" },
                    { 2, "Field is fully replaced with an anonymized placeholder.", "Anonymize" },
                    { 3, "Field is replaced with a SHA-256 hash for pseudonymization.", "Hash" },
                    { 4, "Field is transformed by a custom delegate.", "Custom" }
                });

            migrationBuilder.InsertData(
                schema: "audit",
                table: "RefGdprStorageStates",
                columns: new[] { "Id", "Description", "Name" },
                values: new object[,]
                {
                    { 2, "All sensitive data has been fully anonymized.", "FullyAnonymized" },
                    { 0, "Data is stored unmodified.", "Original" },
                    { 1, "Some fields have been masked, hashed or otherwise processed.", "PartiallyProcessed" },
                    { 3, "Data has been erased under right-to-erasure.", "Erased" }
                });

            migrationBuilder.Operations.Add(new CreateIndexOperation
            {
                Name = "IX_RefAuditActions_Name",
                Schema = "audit",
                Table = "RefAuditActions",
                Columns = new[] { "Name" },
                IsUnique = true
            });

            migrationBuilder.Operations.Add(new CreateIndexOperation
            {
                Name = "IX_RefAuditUserSources_Name",
                Schema = "audit",
                Table = "RefAuditUserSources",
                Columns = new[] { "Name" },
                IsUnique = true
            });

            migrationBuilder.Operations.Add(new CreateIndexOperation
            {
                Name = "IX_RefGdprFieldActions_Name",
                Schema = "audit",
                Table = "RefGdprFieldActions",
                Columns = new[] { "Name" },
                IsUnique = true
            });

            migrationBuilder.Operations.Add(new CreateIndexOperation
            {
                Name = "IX_RefGdprStorageStates_Name",
                Schema = "audit",
                Table = "RefGdprStorageStates",
                Columns = new[] { "Name" },
                IsUnique = true
            });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RefAuditActions",
                schema: "audit");

            migrationBuilder.DropTable(
                name: "RefAuditUserSources",
                schema: "audit");

            migrationBuilder.DropTable(
                name: "RefGdprFieldActions",
                schema: "audit");

            migrationBuilder.DropTable(
                name: "RefGdprStorageStates",
                schema: "audit");
        }
    }
}
