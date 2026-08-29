using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace RzR.DataVigil.Storage.EfPostgreSql.Migrations
{
    public partial class AddAuditActionAndGdprStateIndexes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Operations.Add(new DropIndexOperation
            {
                Name = "IX_AuditEntries_EntityName",
                Schema = "audit",
                Table = "AuditEntries"
            });

            migrationBuilder.Operations.Add(new DropIndexOperation
            {
                Name = "IX_AuditEntries_TransactionId",
                Schema = "audit",
                Table = "AuditEntries"
            });

            migrationBuilder.Operations.Add(new CreateIndexOperation
            {
                Name = "IX_AuditTransactions_GdprState_Timestamp",
                Schema = "audit",
                Table = "AuditTransactions",
                Columns = new[] { "GdprState", "Timestamp" }
            });

            migrationBuilder.Operations.Add(new CreateIndexOperation
            {
                Name = "IX_AuditEntries_EntityName",
                Schema = "audit",
                Table = "AuditEntries",
                Columns = new[] { "EntityName", "Action" }
            });

            migrationBuilder.Operations.Add(new CreateIndexOperation
            {
                Name = "IX_AuditEntries_TransactionId",
                Schema = "audit",
                Table = "AuditEntries",
                Columns = new[] { "TransactionId", "Action" }
            });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Operations.Add(new DropIndexOperation
            {
                Name = "IX_AuditTransactions_GdprState_Timestamp",
                Schema = "audit",
                Table = "AuditTransactions"
            });

            migrationBuilder.Operations.Add(new DropIndexOperation
            {
                Name = "IX_AuditEntries_EntityName",
                Schema = "audit",
                Table = "AuditEntries"
            });

            migrationBuilder.Operations.Add(new DropIndexOperation
            {
                Name = "IX_AuditEntries_TransactionId",
                Schema = "audit",
                Table = "AuditEntries"
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
        }
    }
}
