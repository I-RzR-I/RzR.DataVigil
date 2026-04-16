// ***********************************************************************
//  Assembly         : RzR.DataVigil.EFCore
//  Author           : RzR
//  Created On       : 2026-04-10 23:04
// 
//  Last Modified By : RzR
//  Last Modified On : 2026-04-14 18:10
// ***********************************************************************
//  <copyright file="ChangeTrackerEntryBuilder.cs" company="RzR SOFT & TECH">
//   Copyright © RzR. All rights reserved.
//  </copyright>
// 
//  <summary>
//  </summary>
// ***********************************************************************

#region U S A G E S

using System;
using System.Collections.Generic;
using System.Linq;
using DomainCommonExtensions.ArraysExtensions;
using DomainCommonExtensions.DataTypeExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using RzR.DataVigil.Abstractions.Enums;
using RzR.DataVigil.Abstractions.Models.Entries;

#endregion

namespace RzR.DataVigil.EFCore.Helpers
{
    /// -------------------------------------------------------------------------------------------------
    /// <summary>
    ///     Builds AuditEntry instances from EF Core ChangeTracker entries.
    /// </summary>
    /// =================================================================================================
    internal class ChangeTrackerEntryBuilder
    {
        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Build an AuditEntry from a tracked entity entry. The TransactionId is set later by the
        ///     caller.
        /// </summary>
        /// <param name="entry">The entry.</param>
        /// <param name="excludedFields">(Optional) The excluded fields.</param>
        /// <returns>
        ///     An AuditEntry.
        /// </returns>
        /// =================================================================================================
        public static AuditEntry Build(EntityEntry entry, IList<string> excludedFields = null)
        {
            var auditEntry = new AuditEntry
            {
                Id = Guid.NewGuid(),
                Action = ToAuditAction(entry.State),
                EntityName = entry.Entity.GetType().Name,
                EntityTypeName = entry.Entity.GetType().FullName,
                EntityId = GetPrimaryKeyValue(entry)
            };

            switch (entry.State)
            {
                case EntityState.Added:
                    BuildAddedProperties(entry, auditEntry, excludedFields);
                    break;

                case EntityState.Modified:
                    BuildModifiedProperties(entry, auditEntry, excludedFields);
                    break;

                case EntityState.Deleted:
                    BuildDeletedProperties(entry, auditEntry, excludedFields);
                    break;
            }

            return auditEntry;
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Builds added properties.
        /// </summary>
        /// <param name="entry">The entry.</param>
        /// <param name="auditEntry">The audit entry.</param>
        /// <param name="excludedFields">The excluded fields.</param>
        /// =================================================================================================
        private static void BuildAddedProperties(EntityEntry entry, AuditEntry auditEntry, IList<string> excludedFields)
        {
            foreach (var property in entry.CurrentValues.Properties.NotNull())
            {
                var propertyName = PropertyMetadataHelper.GetName(property);
                if (excludedFields.IsNotNullOrEmptyEnumerable() && excludedFields.Contains(propertyName))
                    continue;

                var currentValue = entry.CurrentValues[property];
                auditEntry.Properties.Add(new AuditEntryProperty
                {
                    PropertyName = propertyName,
                    PropertyType = PropertyMetadataHelper.GetCleanTypeName(PropertyMetadataHelper.GetClrType(property)),
                    OldValue = null,
                    NewValue = currentValue?.ToString()
                });
            }
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Builds modified properties.
        /// </summary>
        /// <param name="entry">The entry.</param>
        /// <param name="auditEntry">The audit entry.</param>
        /// <param name="excludedFields">The excluded fields.</param>
        /// =================================================================================================
        private static void BuildModifiedProperties(EntityEntry entry, AuditEntry auditEntry, IList<string> excludedFields)
        {
            foreach (var property in entry.OriginalValues.Properties.NotNull())
            {
                var propertyName = PropertyMetadataHelper.GetName(property);
                if (excludedFields.IsNotNullOrEmptyEnumerable() && excludedFields.Contains(propertyName))
                    continue;

                var originalValue = entry.OriginalValues[property];
                var currentValue = entry.CurrentValues[property];

                // Only include properties that actually changed
                if (Equals(originalValue, currentValue).IsFalse())
                {
                    auditEntry.Properties.Add(new AuditEntryProperty
                    {
                        PropertyName = propertyName,
                        PropertyType = PropertyMetadataHelper.GetCleanTypeName(PropertyMetadataHelper.GetClrType(property)),
                        OldValue = originalValue?.ToString(),
                        NewValue = currentValue?.ToString()
                    });
                }
            }
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Builds deleted properties.
        /// </summary>
        /// <param name="entry">The entry.</param>
        /// <param name="auditEntry">The audit entry.</param>
        /// <param name="excludedFields">The excluded fields.</param>
        /// =================================================================================================
        private static void BuildDeletedProperties(EntityEntry entry, AuditEntry auditEntry, IList<string> excludedFields)
        {
            foreach (var property in entry.OriginalValues.Properties.NotNull())
            {
                var propertyName = PropertyMetadataHelper.GetName(property);
                if (excludedFields.IsNotNullOrEmptyEnumerable() && excludedFields.Contains(propertyName))
                    continue;

                var originalValue = entry.OriginalValues[property];
                auditEntry.Properties.Add(new AuditEntryProperty
                {
                    PropertyName = propertyName,
                    PropertyType = PropertyMetadataHelper.GetCleanTypeName(PropertyMetadataHelper.GetClrType(property)),
                    OldValue = originalValue?.ToString(),
                    NewValue = null
                });
            }
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Gets primary key value.
        /// </summary>
        /// <param name="entry">The entry.</param>
        /// <returns>
        ///     The primary key value.
        /// </returns>
        /// =================================================================================================
        private static string GetPrimaryKeyValue(EntityEntry entry)
        {
            var keyProperties = entry.Metadata.FindPrimaryKey()?.Properties;
            if (keyProperties.IsNullOrEmptyEnumerable())
                return null;

            if (keyProperties!.Count == 1)
            {
                var value = entry.Property(PropertyMetadataHelper.GetName(keyProperties[0])).CurrentValue;

                return value?.ToString();
            }

            // Composite key
            var parts = new List<string>(keyProperties.Count);
            for (var i = 0; i < keyProperties.Count; i++)
            {
                var value = entry.Property(PropertyMetadataHelper.GetName(keyProperties[i])).CurrentValue;
                parts.Add(value?.ToString() ?? "null");
            }

            return parts.ListToString(",");
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Converts a state to an audit action.
        /// </summary>
        /// <param name="state">The state.</param>
        /// <returns>
        ///     State as an AuditAction.
        /// </returns>
        /// =================================================================================================
        private static AuditAction ToAuditAction(EntityState state)
        {
            switch (state)
            {
                case EntityState.Added:
                    return AuditAction.Create;
                case EntityState.Modified:
                    return AuditAction.Update;
                case EntityState.Deleted:
                    return AuditAction.Delete;
                default:
                    return AuditAction.Read;
            }
        }
    }
}