using Microsoft.EntityFrameworkCore.Migrations;

namespace RzR.DataVigil.Storage.EfPostgreSql.Migrations
{
    public partial class WidenAuditTransactionQueryIndexes : Migration
    {
        /// <inheritdoc/>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "CREATE INDEX CONCURRENTLY IF NOT EXISTS \"IX_AuditTransactions_Timestamp_Id\" ON audit.\"AuditTransactions\" (\"Timestamp\", \"Id\")",
                suppressTransaction: true);

            migrationBuilder.Sql(
                "CREATE INDEX CONCURRENTLY IF NOT EXISTS \"IX_AuditTransactions_UserId_Timestamp_Id\" ON audit.\"AuditTransactions\" (\"UserId\", \"Timestamp\", \"Id\")",
                suppressTransaction: true);

            migrationBuilder.Sql(
                "CREATE INDEX CONCURRENTLY IF NOT EXISTS \"IX_AuditTransactions_CorrelationId_Timestamp_Id\" ON audit.\"AuditTransactions\" (\"CorrelationId\", \"Timestamp\", \"Id\")",
                suppressTransaction: true);

            migrationBuilder.Sql(
                "CREATE INDEX CONCURRENTLY IF NOT EXISTS \"IX_AuditTransactions_GdprState_Timestamp_Id\" ON audit.\"AuditTransactions\" (\"GdprState\", \"Timestamp\", \"Id\")",
                suppressTransaction: true);
        }

        /// <inheritdoc/>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DROP INDEX CONCURRENTLY IF EXISTS audit.\"IX_AuditTransactions_GdprState_Timestamp_Id\"",
                suppressTransaction: true);

            migrationBuilder.Sql(
                "DROP INDEX CONCURRENTLY IF EXISTS audit.\"IX_AuditTransactions_CorrelationId_Timestamp_Id\"",
                suppressTransaction: true);

            migrationBuilder.Sql(
                "DROP INDEX CONCURRENTLY IF EXISTS audit.\"IX_AuditTransactions_UserId_Timestamp_Id\"",
                suppressTransaction: true);

            migrationBuilder.Sql(
                "DROP INDEX CONCURRENTLY IF EXISTS audit.\"IX_AuditTransactions_Timestamp_Id\"",
                suppressTransaction: true);
        }
    }
}
