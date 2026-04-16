using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using System;

namespace RzR.DataVigil.Storage.EfSqlServer.Migrations
{
    public partial class InitAuditCommit : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "audit");

            migrationBuilder.CreateTable(
                name: "AuditTransactions",
                schema: "audit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    TraceId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Source = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    GdprState = table.Column<int>(type: "int", nullable: false),
                    Metadata = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditTransactions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuditEntries",
                schema: "audit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<int>(type: "int", nullable: false),
                    EntityName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EntityId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EntityTypeName = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditEntries_AuditTransactions_TransactionId",
                        column: x => x.TransactionId,
                        principalSchema: "audit",
                        principalTable: "AuditTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AuditEntryProperties",
                schema: "audit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PropertyName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    PropertyType = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    OldValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NewValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AuditEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEntryProperties", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditEntryProperties_AuditEntries_AuditEntryId",
                        column: x => x.AuditEntryId,
                        principalSchema: "audit",
                        principalTable: "AuditEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Operations.Add(new CreateIndexOperation
            {
                Name = "IX_AuditEntries_EntityName",
                Schema = "audit",
                Table = "AuditEntries",
                Columns = new[] { "EntityName" }
            });

            migrationBuilder.Operations.Add(new CreateIndexOperation
            {
                Name = "IX_AuditEntries_TransactionId",
                Schema = "audit",
                Table = "AuditEntries",
                Columns = new[] { "TransactionId" }
            });

            migrationBuilder.Operations.Add(new CreateIndexOperation
            {
                Name = "IX_AuditEntryProperties_AuditEntryId",
                Schema = "audit",
                Table = "AuditEntryProperties",
                Columns = new[] { "AuditEntryId" }
            });

            migrationBuilder.Operations.Add(new CreateIndexOperation
            {
                Name = "IX_AuditTransactions_CorrelationId",
                Schema = "audit",
                Table = "AuditTransactions",
                Columns = new[] { "CorrelationId" }
            });

            migrationBuilder.Operations.Add(new CreateIndexOperation
            {
                Name = "IX_AuditTransactions_Timestamp",
                Schema = "audit",
                Table = "AuditTransactions",
                Columns = new[] { "Timestamp" }
            });

            migrationBuilder.Operations.Add(new CreateIndexOperation
            {
                Name = "IX_AuditTransactions_UserId",
                Schema = "audit",
                Table = "AuditTransactions",
                Columns = new[] { "UserId" }
            });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditEntryProperties",
                schema: "audit");

            migrationBuilder.DropTable(
                name: "AuditEntries",
                schema: "audit");

            migrationBuilder.DropTable(
                name: "AuditTransactions",
                schema: "audit");
        }
    }
}
