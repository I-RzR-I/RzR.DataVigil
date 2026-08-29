using Microsoft.EntityFrameworkCore.Migrations;

namespace RzR.DataVigil.Storage.EfSqlServer.Migrations
{
    public partial class WidenAuditTransactionQueryIndexes : Migration
    {
        /// <inheritdoc/>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                @"
DECLARE @online nvarchar(3) = CASE WHEN CAST(SERVERPROPERTY('EngineEdition') AS int) IN (3, 5, 8) THEN N'ON' ELSE N'OFF' END;
DECLARE @sql nvarchar(max);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AuditTransactions_Timestamp_Id' AND object_id = OBJECT_ID(N'[audit].[AuditTransactions]'))
BEGIN
    SET @sql = N'CREATE NONCLUSTERED INDEX [IX_AuditTransactions_Timestamp_Id] ON [audit].[AuditTransactions] ([Timestamp], [Id]) WITH (ONLINE = ' + @online + N');';
    EXEC sp_executesql @sql;
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AuditTransactions_UserId_Timestamp_Id' AND object_id = OBJECT_ID(N'[audit].[AuditTransactions]'))
BEGIN
    SET @sql = N'CREATE NONCLUSTERED INDEX [IX_AuditTransactions_UserId_Timestamp_Id] ON [audit].[AuditTransactions] ([UserId], [Timestamp], [Id]) WITH (ONLINE = ' + @online + N');';
    EXEC sp_executesql @sql;
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AuditTransactions_CorrelationId_Timestamp_Id' AND object_id = OBJECT_ID(N'[audit].[AuditTransactions]'))
BEGIN
    SET @sql = N'CREATE NONCLUSTERED INDEX [IX_AuditTransactions_CorrelationId_Timestamp_Id] ON [audit].[AuditTransactions] ([CorrelationId], [Timestamp], [Id]) WITH (ONLINE = ' + @online + N');';
    EXEC sp_executesql @sql;
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AuditTransactions_GdprState_Timestamp_Id' AND object_id = OBJECT_ID(N'[audit].[AuditTransactions]'))
BEGIN
    SET @sql = N'CREATE NONCLUSTERED INDEX [IX_AuditTransactions_GdprState_Timestamp_Id] ON [audit].[AuditTransactions] ([GdprState], [Timestamp], [Id]) WITH (ONLINE = ' + @online + N');';
    EXEC sp_executesql @sql;
END;
",
                suppressTransaction: true);
        }

        /// <inheritdoc/>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                @"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AuditTransactions_GdprState_Timestamp_Id' AND object_id = OBJECT_ID(N'[audit].[AuditTransactions]'))
    DROP INDEX [IX_AuditTransactions_GdprState_Timestamp_Id] ON [audit].[AuditTransactions];

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AuditTransactions_CorrelationId_Timestamp_Id' AND object_id = OBJECT_ID(N'[audit].[AuditTransactions]'))
    DROP INDEX [IX_AuditTransactions_CorrelationId_Timestamp_Id] ON [audit].[AuditTransactions];

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AuditTransactions_UserId_Timestamp_Id' AND object_id = OBJECT_ID(N'[audit].[AuditTransactions]'))
    DROP INDEX [IX_AuditTransactions_UserId_Timestamp_Id] ON [audit].[AuditTransactions];

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AuditTransactions_Timestamp_Id' AND object_id = OBJECT_ID(N'[audit].[AuditTransactions]'))
    DROP INDEX [IX_AuditTransactions_Timestamp_Id] ON [audit].[AuditTransactions];
",
                suppressTransaction: true);
        }
    }
}
