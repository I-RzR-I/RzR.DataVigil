using Microsoft.EntityFrameworkCore.Migrations;

namespace RzR.DataVigil.Storage.EfPostgreSql.Migrations
{
    public partial class DropLegacyAuditTransactionIndexes : Migration
    {
        /// <inheritdoc/>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                @"DO $$
DECLARE
    unusable text;
BEGIN
    SELECT string_agg(expected.name, ', ' ORDER BY expected.name)
      INTO unusable
      FROM (VALUES
                ('IX_AuditTransactions_Timestamp_Id'),
                ('IX_AuditTransactions_UserId_Timestamp_Id'),
                ('IX_AuditTransactions_CorrelationId_Timestamp_Id'),
                ('IX_AuditTransactions_GdprState_Timestamp_Id')
           ) AS expected(name)
      LEFT JOIN pg_class c
             ON c.relname = expected.name
            AND c.relnamespace = 'audit'::regnamespace
      LEFT JOIN pg_index i
             ON i.indexrelid = c.oid
     WHERE c.oid IS NULL OR NOT i.indisvalid;

    IF unusable IS NOT NULL THEN
        RAISE EXCEPTION
            'Refusing to drop the legacy audit indexes: replacement index(es) % are missing or INVALID. '
            'Rebuild them with REINDEX INDEX CONCURRENTLY, or DROP INDEX CONCURRENTLY and re-apply '
            'WidenAuditTransactionQueryIndexes, then run this migration again. The legacy indexes have '
            'been left in place.', unusable;
    END IF;
END $$;");

            migrationBuilder.Sql(
                "DROP INDEX CONCURRENTLY IF EXISTS audit.\"IX_AuditTransactions_Timestamp\"",
                suppressTransaction: true);

            migrationBuilder.Sql(
                "DROP INDEX CONCURRENTLY IF EXISTS audit.\"IX_AuditTransactions_UserId\"",
                suppressTransaction: true);

            migrationBuilder.Sql(
                "DROP INDEX CONCURRENTLY IF EXISTS audit.\"IX_AuditTransactions_CorrelationId\"",
                suppressTransaction: true);

            migrationBuilder.Sql(
                "DROP INDEX CONCURRENTLY IF EXISTS audit.\"IX_AuditTransactions_GdprState_Timestamp\"",
                suppressTransaction: true);
        }

        /// <inheritdoc/>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "CREATE INDEX CONCURRENTLY IF NOT EXISTS \"IX_AuditTransactions_GdprState_Timestamp\" ON audit.\"AuditTransactions\" (\"GdprState\", \"Timestamp\")",
                suppressTransaction: true);

            migrationBuilder.Sql(
                "CREATE INDEX CONCURRENTLY IF NOT EXISTS \"IX_AuditTransactions_CorrelationId\" ON audit.\"AuditTransactions\" (\"CorrelationId\")",
                suppressTransaction: true);

            migrationBuilder.Sql(
                "CREATE INDEX CONCURRENTLY IF NOT EXISTS \"IX_AuditTransactions_UserId\" ON audit.\"AuditTransactions\" (\"UserId\")",
                suppressTransaction: true);

            migrationBuilder.Sql(
                "CREATE INDEX CONCURRENTLY IF NOT EXISTS \"IX_AuditTransactions_Timestamp\" ON audit.\"AuditTransactions\" (\"Timestamp\")",
                suppressTransaction: true);
        }
    }
}
