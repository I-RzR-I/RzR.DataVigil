// ***********************************************************************
//  Assembly         : RzR.DataVigil.Storage.EfMongoDb
//  Author           : RzR
//  Created On       : 2026-04-15 18:04
// 
//  Last Modified By : RzR
//  Last Modified On : 2026-04-15 18:04
// ***********************************************************************
//  <copyright file="AuditMaterializationInterceptor.cs" company="RzR SOFT & TECH">
//   Copyright © RzR. All rights reserved.
//  </copyright>
// 
//  <summary>
//  </summary>
// ***********************************************************************

#region U S A G E S

using System;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
using RzR.DataVigil.Abstractions.Contracts;
using RzR.DataVigil.Abstractions.Enums;
using RzR.DataVigil.Abstractions.Models.Entries;
using RzR.DataVigil.Core.Options;
using RzR.DataVigil.Core.Pipeline;
using RzR.Extensions.Domain.Primitives;

#endregion

namespace RzR.DataVigil.Storage.EfMongoDb.Interceptors
{
    /// -------------------------------------------------------------------------------------------------
    /// <summary>
    ///     EF Core materialization interceptor that automatically captures Read audit entries for
    ///     every <see cref="IAuditable"/> entity materialized during a query.
    ///     <para>
    ///     This interceptor works with all EF Core providers (including non-relational ones like
    ///     MongoDB) because it hooks into entity materialization — not SQL execution.
    ///     </para>
    ///     <para>
    ///     Entries are collected synchronously in the <see cref="AuditReadCollector"/> and flushed
    ///     asynchronously at the end of the HTTP request by middleware.
    ///     </para>
    /// </summary>
    /// <seealso cref="T:Microsoft.EntityFrameworkCore.Diagnostics.IMaterializationInterceptor"/>
    /// =================================================================================================
    public sealed class AuditMaterializationInterceptor : IMaterializationInterceptor
    {
        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     (Immutable) options for controlling the operation.
        /// </summary>
        /// =================================================================================================
        private readonly AuditTrailOptions _options;

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     (Immutable) the collector.
        /// </summary>
        /// =================================================================================================
        private readonly AuditReadCollector _collector;

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Initializes a new instance of the <see cref="AuditMaterializationInterceptor"/> class.
        /// </summary>
        /// <param name="options">The audit trail options.</param>
        /// <param name="collector">The scoped read collector.</param>
        /// =================================================================================================
        public AuditMaterializationInterceptor(
            AuditTrailOptions options,
            AuditReadCollector collector)
        {
            _options = options;
            _collector = collector;
        }

        /// <inheritdoc/>
        public InterceptionResult<object> CreatingInstance(
            MaterializationInterceptionData materializationData,
            InterceptionResult<object> result)
            => result;

        /// <inheritdoc/>
        public object CreatedInstance(
            MaterializationInterceptionData materializationData,
            object entity)
            => entity;

        /// <inheritdoc/>
        public object InitializedInstance(
            MaterializationInterceptionData materializationData,
            object entity)
        {
            if (entity.IsNull())
                return entity;

            if (!_options.EfCore.IncludeReadsEnabled)
                return entity;

            if (!(entity is IAuditable))
                return entity;

            var clrType = entity.GetType();

            // Global exclusions
            if (_options.GlobalExclusions.Contains(clrType))
                return entity;

            var context = materializationData.Context;
            if (context.IsNull())
                return entity;

            if (!_options.EfCore.ShouldAuditContext(context.GetType()))
                return entity;

            // Skip audit storage context to prevent infinite recursion
            if (context is AuditMongoDbContext)
                return entity;

            var entityType = materializationData.EntityType;
            var transactionId = Guid.NewGuid();

            var auditEntry = new AuditEntry
            {
                Id = Guid.NewGuid(),
                TransactionId = transactionId,
                Action = AuditAction.Read,
                EntityName = clrType.Name,
                EntityTypeName = clrType.FullName,
                EntityId = GetPrimaryKeyValue(entityType, entity)
            };

            if (_options.EfCore.IncludeReadPropertiesEnabled)
                BuildAllReadProperties(entityType, auditEntry, entity);

            _collector.Collect(auditEntry);

            return entity;
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Extracts the primary key value from an entity instance using EF Core model metadata.
        /// </summary>
        /// <param name="entityType">Type of the entity.</param>
        /// <param name="entity">The entity.</param>
        /// <returns>
        ///     The primary key value.
        /// </returns>
        /// =================================================================================================
        private static string GetPrimaryKeyValue(IEntityType entityType, object entity)
        {
            var keyProperties = entityType.FindPrimaryKey()?.Properties;
            if (keyProperties == null || keyProperties.Count == 0)
                return null;

            if (keyProperties.Count == 1)
            {
                var propName = keyProperties[0].Name;
                var propInfo = entity.GetType().GetProperty(propName, BindingFlags.Public | BindingFlags.Instance);

                return propInfo?.GetValue(entity)?.ToString();
            }

            var parts = new string[keyProperties.Count];
            for (var i = 0; i < keyProperties.Count; i++)
            {
                var propName = keyProperties[i].Name;
                var propInfo = entity.GetType().GetProperty(propName, BindingFlags.Public | BindingFlags.Instance);
                parts[i] = propInfo?.GetValue(entity)?.ToString() ?? "null";
            }

            return string.Join(",", parts);
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Builds AuditEntryProperty records for all properties of the entity type.
        /// </summary>
        /// <param name="entityType">Type of the entity.</param>
        /// <param name="auditEntry">The audit entry.</param>
        /// <param name="entity">The entity.</param>
        /// =================================================================================================
        private void BuildAllReadProperties(IEntityType entityType, AuditEntry auditEntry, object entity)
        {
            foreach (var property in entityType.GetProperties())
            {
                var propertyName = property.Name;
                var propertyType = property.ClrType;
                var underlyingType = propertyType != null ? Nullable.GetUnderlyingType(propertyType) : null;
                var cleanTypeName = underlyingType != null
                    ? underlyingType.FullName + "?"
                    : propertyType?.FullName;

                string value = null;
                if (_options.EfCore.IncludeReadPropertiesValueEnabled)
                {
                    var propInfo = entity.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
                    value = propInfo?.GetValue(entity)?.ToString();
                }

                auditEntry.Properties.Add(new AuditEntryProperty
                {
                    PropertyName = propertyName,
                    PropertyType = cleanTypeName,
                    OldValue = null,
                    NewValue = value
                });
            }
        }
    }
}
