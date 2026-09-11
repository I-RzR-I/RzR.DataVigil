// ***********************************************************************
//  Assembly         : RzR.DataVigil.EFCore
//  Author           : RzR
//  Created On       : 2026-04-15 13:04
// 
//  Last Modified By : RzR
//  Last Modified On : 2026-09-11 21:30
// ***********************************************************************
//  <copyright file="AuditReadService.cs" company="RzR SOFT & TECH">
//   Copyright © RzR. All rights reserved.
//  </copyright>
// 
//  <summary>
//  </summary>
// ***********************************************************************

#region U S A G E S

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Logging;
using RzR.DataVigil.Abstractions.Contracts;
using RzR.DataVigil.Abstractions.Enums;
using RzR.DataVigil.Abstractions.Extensions;
using RzR.DataVigil.Abstractions.Models.Entries;
using RzR.DataVigil.Core.Options;
using RzR.DataVigil.Core.Pipeline;
using RzR.DataVigil.EFCore.Helpers;
using RzR.Extensions.Domain.Collections;
using RzR.Extensions.Domain.Primitives;

#endregion

namespace RzR.DataVigil.EFCore
{
    /// -------------------------------------------------------------------------------------------------
    /// <summary>
    ///     Provides explicit Read audit logging for EF Core providers that do not support
    ///     <c>DbCommandInterceptor</c> (e.g. MongoDB, Cosmos, in-memory).
    ///     For relational providers (SQL Server, PostgreSQL) reads are audited automatically via the
    ///     <see cref="Interceptors.AuditCommandInterceptor" />.
    ///     <para>
    ///         Usage:
    ///         <code>
    ///     var posts = await _db.Posts.ToListAsync();
    ///     await _auditRead.LogReadAsync&lt;Post&gt;(_db, posts, cancellationToken);
    ///     </code>
    ///     </para>
    /// </summary>
    /// =================================================================================================
    public sealed class AuditReadService
    {
        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     (Immutable) the logger.
        /// </summary>
        /// =================================================================================================
        private readonly ILogger<AuditReadService> _logger;

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
        ///     Initializes a new instance of the <see cref="AuditReadService" /> class.
        /// </summary>
        /// <param name="options">The audit trail options.</param>
        /// <param name="pipeline">The audit pipeline.</param>
        /// <param name="logger">The logger.</param>
        /// =================================================================================================
        public AuditReadService(
            AuditTrailOptions options,
            AuditPipeline pipeline,
            ILogger<AuditReadService> logger)
        {
            _options = options;
            _pipeline = pipeline;
            _logger = logger;
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Logs a Read audit entry for a single entity. The entity's primary key is extracted from
        ///     the EF Core model metadata.
        /// </summary>
        /// <typeparam name="TEntity">
        ///     The entity type (must implement <see cref="IAuditable" />).
        /// </typeparam>
        /// <param name="context">The DbContext that owns the entity model.</param>
        /// <param name="entity">The entity instance that was read.</param>
        /// <param name="cancellationToken">(Optional) A token to cancel the operation.</param>
        /// <returns>
        ///     A Task representing the async operation.
        /// </returns>
        /// =================================================================================================
        public Task LogReadAsync<TEntity>(DbContext context, TEntity entity,
            CancellationToken cancellationToken = default)
            where TEntity : class, IAuditable
        {
            return LogReadInternalAsync(context, typeof(TEntity), new object[] { entity }, cancellationToken);
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Logs Read audit entries for a collection of entities. One audit entry per entity is
        ///     created within a single transaction, each with its primary key extracted from the EF Core
        ///     model.
        /// </summary>
        /// <typeparam name="TEntity">
        ///     The entity type (must implement <see cref="IAuditable" />).
        /// </typeparam>
        /// <param name="context">The DbContext that owns the entity model.</param>
        /// <param name="entities">The entity instances that were read.</param>
        /// <param name="cancellationToken">(Optional) A token to cancel the operation.</param>
        /// <returns>
        ///     A Task representing the async operation.
        /// </returns>
        /// =================================================================================================
        public Task LogReadAsync<TEntity>(DbContext context, IEnumerable<TEntity> entities,
            CancellationToken cancellationToken = default)
            where TEntity : class, IAuditable
        {
            return LogReadInternalAsync(context, typeof(TEntity), entities, cancellationToken);
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Internal implementation that builds and processes Read audit entries.
        /// </summary>
        /// <param name="context">The DbContext that owns the entity model.</param>
        /// <param name="clrType">Type of the colour.</param>
        /// <param name="entities">The entity instances that were read.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>
        ///     A Task.
        /// </returns>
        /// =================================================================================================
        private async Task LogReadInternalAsync(DbContext context, Type clrType,
            IEnumerable entities, CancellationToken cancellationToken)
        {
            try
            {
                if (_options.EfCore.IncludeReadsEnabled.IsFalse())
                    return;

                if (_options.EfCore.ShouldAuditContext(context.GetType()).IsFalse())
                    return;

                // Global exclusions
                if (_options.GlobalExclusions.Contains(clrType))
                    return;

                IEntityType entityType = null;
                foreach (var candidate in context.Model.GetEntityTypes())
                {
                    if (PropertyMetadataHelper.GetClrType(candidate) != clrType)
                        continue;

                    entityType = candidate;
                    break;
                }

                if (entityType == null)
                    return;

                var transactionId = Guid.NewGuid();
                var auditEntries = new List<AuditEntry>();

                foreach (var entity in entities)
                {
                    if (entity == null)
                        continue;

                    var entityId = GetPrimaryKeyValue(entityType, entity);

                    var auditEntry = new AuditEntry
                    {
                        Id = Guid.NewGuid(),
                        TransactionId = transactionId,
                        Action = AuditAction.Read,
                        EntityName = clrType.Name,
                        EntityTypeName = clrType.FullName,
                        EntityId = entityId
                    };

                    if (_options.EfCore.IncludeReadPropertiesEnabled)
                        BuildAllReadProperties(entityType, auditEntry, entity);

                    auditEntries.Add(auditEntry);
                }

                if (auditEntries.IsNullOrEmptyEnumerable())
                    return;

                var transaction = new AuditTransaction
                {
                    Id = transactionId,
                    Timestamp = DateTimeOffset.UtcNow,
                    Entries = auditEntries
                };

                var result = await _pipeline.ProcessAsync(transaction, cancellationToken).ConfigureAwait(false);
                if (result.IsFailure)
                {
                    if (result.IsAuditCanceled())
                        _logger.LogDebug(
                            "Audit pipeline was canceled for Read on entity {Entity}.",
                            clrType.Name);
                    else
                        _logger.LogWarning(
                            "Audit pipeline failed for Read on entity {Entity}.",
                            clrType.Name);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to audit Read operation for entity {Entity}.", clrType.Name);
            }
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Extracts the primary key value from an entity instance using EF Core model metadata. 
        /// </summary>
        /// <param name="entityType">Type of the entity.</param>
        /// <param name="entity">The entity instance that was read.</param>
        /// <returns>
        ///     The primary key value.
        /// </returns>
        /// =================================================================================================
        private static string GetPrimaryKeyValue(IEntityType entityType, object entity)
        {
            var keyProperties = entityType.FindPrimaryKey()?.Properties;
            if (keyProperties.IsNullOrEmptyEnumerable())
                return null;

            if (keyProperties!.Count == 1)
            {
                var propName = PropertyMetadataHelper.GetName(keyProperties[0]);
                var propInfo = entity.GetType().GetProperty(propName, BindingFlags.Public | BindingFlags.Instance);

                return ChangeTrackerEntryBuilder.ToAuditKeyValue(keyProperties[0], propInfo?.GetValue(entity));
            }

            // Composite key
            var parts = new List<string>(keyProperties.Count);
            for (var i = 0; i < keyProperties.Count; i++)
            {
                var propName = PropertyMetadataHelper.GetName(keyProperties[i]);
                var propInfo = entity.GetType().GetProperty(propName, BindingFlags.Public | BindingFlags.Instance);
                parts.Add(ChangeTrackerEntryBuilder.ToAuditKeyValue(keyProperties[i], propInfo?.GetValue(entity)) ?? "null");
            }

            return string.Join(",", parts);
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Builds AuditEntryProperty records for all properties of the entity type. 
        ///     When <see cref="EfCoreAuditOptions.IncludeReadPropertiesValueEnabled" />
        ///     is set, reads the actual property values from the entity instance.
        /// </summary>
        /// <param name="entityType">Type of the entity.</param>
        /// <param name="auditEntry">The audit entry.</param>
        /// <param name="entity">The entity instance that was read.</param>
        /// =================================================================================================
        private void BuildAllReadProperties(IEntityType entityType, AuditEntry auditEntry, object entity)
        {
            foreach (var property in entityType.GetProperties())
            {
                var propertyName = PropertyMetadataHelper.GetName(property);
                var propertyType = PropertyMetadataHelper.GetClrType(property);

                string value = null;
                if (_options.EfCore.IncludeReadPropertiesValueEnabled && entity != null)
                {
                    var propInfo = entity.GetType()
                        .GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
                    value = ChangeTrackerEntryBuilder.ToAuditValue(property, propInfo?.GetValue(entity));
                }

                auditEntry.Properties.Add(new AuditEntryProperty
                {
                    PropertyName = propertyName,
                    PropertyType = PropertyMetadataHelper.GetCleanTypeName(propertyType),
                    OldValue = null,
                    NewValue = value
                });
            }
        }
    }
}