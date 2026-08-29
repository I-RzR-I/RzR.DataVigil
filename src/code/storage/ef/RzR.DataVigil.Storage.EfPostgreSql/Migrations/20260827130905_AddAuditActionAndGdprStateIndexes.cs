using Microsoft.EntityFrameworkCore.Migrations;

namespace RzR.DataVigil.Storage.EfPostgreSql.Migrations
{
    public partial class AddAuditActionAndGdprStateIndexes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AuditEntries_EntityName",
                schema: "audit",
                table: "AuditEntries");

            migrationBuilder.DropIndex(
                name: "IX_AuditEntries_TransactionId",
                schema: "audit",
                table: "AuditEntries");

            migrationBuilder.CreateIndex(
                name: "IX_AuditTransactions_GdprState_Timestamp",
                schema: "audit",
                table: "AuditTransactions",
                columns: new[] { "GdprState", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_EntityName",
                schema: "audit",
                table: "AuditEntries",
                columns: new[] { "EntityName", "Action" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_TransactionId",
                schema: "audit",
                table: "AuditEntries",
                columns: new[] { "TransactionId", "Action" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AuditTransactions_GdprState_Timestamp",
                schema: "audit",
                table: "AuditTransactions");

            migrationBuilder.DropIndex(
                name: "IX_AuditEntries_EntityName",
                schema: "audit",
                table: "AuditEntries");

            migrationBuilder.DropIndex(
                name: "IX_AuditEntries_TransactionId",
                schema: "audit",
                table: "AuditEntries");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_EntityName",
                schema: "audit",
                table: "AuditEntries",
                column: "EntityName");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_TransactionId",
                schema: "audit",
                table: "AuditEntries",
                column: "TransactionId");
        }
    }
}
