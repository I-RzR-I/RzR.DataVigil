// ***********************************************************************
//  Assembly         : RzR.DataVigil.EFCore
//  Author           : RzR
//  Created On       : 2026-04-10 23:04
// 
//  Last Modified By : RzR
//  Last Modified On : 2026-04-14 19:03
// ***********************************************************************
//  <copyright file="AuditCommandInterceptor.cs" company="RzR SOFT & TECH">
//   Copyright © RzR. All rights reserved.
//  </copyright>
// 
//  <summary>
//  </summary>
// ***********************************************************************

#region U S A G E S

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using RzR.DataVigil.Abstractions.Contracts;
using RzR.DataVigil.Abstractions.Enums;
using RzR.DataVigil.Abstractions.Models.Entries;
using RzR.DataVigil.Core.Options;
using RzR.DataVigil.Core.Pipeline;
using RzR.DataVigil.EFCore.Helpers;
using RzR.Extensions.Domain.Collections;
using RzR.Extensions.Domain.Primitives;
using RzR.Extensions.Domain.Text;

#endregion

namespace RzR.DataVigil.EFCore.Interceptors
{
    /// -------------------------------------------------------------------------------------------------
    /// <summary>
    ///     EF Core command interceptor for auditing Read (SELECT) operations. 
    ///     Only active when IncludeReads() and IncludeReadProperties() is configured.
    /// </summary>
    /// <seealso cref="T:Microsoft.EntityFrameworkCore.Diagnostics.DbCommandInterceptor"/>
    /// =================================================================================================
    public sealed class AuditCommandInterceptor : DbCommandInterceptor
    {
        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     (Immutable) the logger.
        /// </summary>
        /// =================================================================================================
        private readonly ILogger<AuditCommandInterceptor> _logger;

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     (Immutable) options for controlling the operation.
        /// </summary>
        /// =================================================================================================
        private readonly AuditTrailOptions _options;

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     (Immutable) the pipeline.
        /// </summary>
        /// =================================================================================================
        private readonly AuditPipeline _pipeline;

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Initializes a new instance of the <see cref="AuditCommandInterceptor"/> class.
        /// </summary>
        /// <param name="options">Options for controlling the operation.</param>
        /// <param name="pipeline">The pipeline.</param>
        /// <param name="logger">The logger.</param>
        /// =================================================================================================
        public AuditCommandInterceptor(
            AuditTrailOptions options,
            AuditPipeline pipeline,
            ILogger<AuditCommandInterceptor> logger)
        {
            _options = options;
            _pipeline = pipeline;
            _logger = logger;
        }

        /// <inheritdoc/>
        public override async ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command,
            CommandExecutedEventData eventData,
            DbDataReader result,
            CancellationToken cancellationToken = default)
        {
            if (_options.EfCore.IncludeReadsEnabled && eventData.Context.IsNotNull())
                await AuditReadAsync(command, eventData.Context, cancellationToken).ConfigureAwait(false);

            return await base.ReaderExecutedAsync(command, eventData, result, cancellationToken)
                .ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public override DbDataReader ReaderExecuted(
            DbCommand command,
            CommandExecutedEventData eventData,
            DbDataReader result)
        {
            if (_options.EfCore.IncludeReadsEnabled && eventData.Context.IsNotNull())
                AuditReadAsync(command, eventData.Context, CancellationToken.None)
                    .ConfigureAwait(false)
                    .GetAwaiter()
                    .GetResult();

            return base.ReaderExecuted(command, eventData, result);
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Audit read asynchronous.
        /// </summary>
        /// <param name="command">The command.</param>
        /// <param name="context">The context.</param>
        /// <param name="cancellationToken">A token that allows processing to be cancelled.</param>
        /// <returns>
        ///     A Task.
        /// </returns>
        /// =================================================================================================
        private async Task AuditReadAsync(DbCommand command, DbContext context, CancellationToken cancellationToken)
        {
            try
            {
                // Guard: never audit reads from the audit storage context itself (prevents infinite recursion)
                if (context is AuditDbContextBase)
                    return;

                if (!_options.EfCore.ShouldAuditContext(context.GetType()))
                    return;

                var auditableCtx = context as IAuditableContext;
                if (auditableCtx.IsNull())
                    return;

                var sql = command.CommandText;
                if (sql.IsMissing())
                    return;

                // Only audit actual SELECT queries — skip DELETE, UPDATE, INSERT
                if (sql.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase).IsFalse())
                    return;

                var tableNames = AuditReadInterceptorHelper.ParseTableNames(sql);
                if (tableNames.IsNullOrEmptyEnumerable())
                    return;

                var excludedEntityTypes = new HashSet<Type>(auditableCtx!.GetExcludedEntityTypes().NotNull());

                var transactionId = Guid.NewGuid();
                var entries = new List<AuditEntry>();

                // Extract entity ID from WHERE clause parameters
                var entityId = AuditReadInterceptorHelper.ExtractEntityId(sql, command.Parameters);

                // Extract selected columns from the SELECT clause (before FROM)
                var selectedColumns = _options.EfCore.IncludeReadPropertiesEnabled
                    ? AuditReadInterceptorHelper.ParseSelectedColumns(sql)
                    : null;

                foreach (var (schema, table) in tableNames.NotNull())
                {
                    var entityType = AuditReadInterceptorHelper.ResolveEntityType(context, schema, table);
                    if (entityType.IsNull())
                        continue;

                    var clrType = PropertyMetadataHelper.GetClrType(entityType);

                    // Must implement IAuditable
                    if (!typeof(IAuditable).IsAssignableFrom(clrType))
                        continue;

                    // Global exclusions
                    if (_options.GlobalExclusions.Contains(clrType))
                        continue;

                    // Context-level exclusions
                    if (excludedEntityTypes.Contains(clrType))
                        continue;

                    var auditEntry = new AuditEntry
                    {
                        Id = Guid.NewGuid(),
                        TransactionId = transactionId,
                        Action = AuditAction.Read,
                        EntityName = clrType.Name,
                        EntityTypeName = clrType.FullName,
                        EntityId = entityId
                    };

                    // Add properties if enabled
                    if (_options.EfCore.IncludeReadPropertiesEnabled && selectedColumns.IsNotNullOrEmptyEnumerable())
                        AuditReadInterceptorHelper.BuildReadProperties(
                            entityType, selectedColumns, auditEntry,
                            command.Parameters, _options.EfCore.IncludeReadPropertiesValueEnabled);

                    entries.Add(auditEntry);
                }

                if (entries.IsNullOrEmptyEnumerable())
                    return;

                var transaction = new AuditTransaction
                {
                    Id = transactionId,
                    Timestamp = DateTimeOffset.UtcNow,
                    Entries = entries
                };

                var auditResult = await _pipeline.ProcessAsync(transaction, cancellationToken).ConfigureAwait(false);
                if (auditResult.IsFailure)
                    _logger.LogWarning(
                        "Audit pipeline failed for Read on context {Context}.",
                        context.GetType().Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to audit Read operation.");
            }
        }
    }
}