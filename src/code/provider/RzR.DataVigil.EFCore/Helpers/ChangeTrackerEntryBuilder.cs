// ***********************************************************************
//  Assembly         : RzR.DataVigil.EFCore
//  Author           : RzR
//  Created On       : 2026-04-10 23:04
// 
//  Last Modified By : RzR
//  Last Modified On : 2026-09-11 21:30
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
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using RzR.DataVigil.Abstractions.Enums;
using RzR.DataVigil.Abstractions.Models.Entries;
using RzR.DataVigil.Core.Helpers;
using RzR.Extensions.Domain.Collections;
using RzR.Extensions.Domain.Primitives;

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
                    NewValue = ToAuditValue(property, currentValue)
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

                var originalModelValue = entry.OriginalValues[property];
                var currentModelValue = entry.CurrentValues[property];

                if (ValuesDiffer(property, originalModelValue, currentModelValue).IsFalse())
                    continue;

                auditEntry.Properties.Add(new AuditEntryProperty
                {
                    PropertyName = propertyName,
                    PropertyType = PropertyMetadataHelper.GetCleanTypeName(PropertyMetadataHelper.GetClrType(property)),
                    OldValue = ToAuditValue(property, originalModelValue),
                    NewValue = ToAuditValue(property, currentModelValue)
                });
            }
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Decides whether a property changed, comparing the MODEL values rather than their formatted
        ///     audit representation, matching EF Core's own change-detection semantics (EF marks a property
        ///     modified by comparing model values through the property's <c>ValueComparer</c>).
        /// </summary>
        /// <param name="property">The EF Core property metadata.</param>
        /// <param name="original">The original model value.</param>
        /// <param name="current">The current model value.</param>
        /// <returns>
        ///     True when the property changed, false when it did not.
        /// </returns>
        /// =================================================================================================
        private static bool ValuesDiffer(object property, object original, object current)
        {
            var comparer = PropertyMetadataHelper.GetValueComparer(property);
            if (comparer.IsNotNull())
            {
                try
                {
                    return PropertyMetadataHelper.ComparerEquals(comparer, original, current).IsFalse();
                }
                catch (Exception)
                {
                    /* ignored */
                }
            }

            if (original.IsNull() && current.IsNull())
                return false;

            if (original.IsNull() || current.IsNull())
                return true;

            if (original is byte[] originalBytes && current is byte[] currentBytes)
                return originalBytes.SequenceEqual(currentBytes).IsFalse();

            return Equals(original, current).IsFalse();
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
                    OldValue = ToAuditValue(property, originalValue),
                    NewValue = null
                });
            }
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Converts a property value to the form recorded in the audit trail. 
        /// </summary>
        /// <param name="property">The EF Core property metadata.</param>
        /// <param name="value">The property value.</param>
        /// <returns>
        ///     The recorded value, or <c>null</c> when the value is <c>null</c>.
        /// </returns>
        /// =================================================================================================
        internal static string ToAuditValue(object property, object value)
        {
            if (value.IsNull())
                return null;

            var clrType = value.GetType();
            if (clrType.IsEnum)
                return value.ToString();

            var converter = PropertyMetadataHelper.GetValueConverter(property);
            if (converter.IsNull())
                return AuditValueFormatter.Format(value);

            try
            {
                var providerValue = PropertyMetadataHelper.ConvertToProvider(converter, value);

                return AuditValueFormatter.Format(providerValue);
            }
            catch (Exception ex)
            {
                return "[unrecordable: converter threw " + ex.GetType().Name + "]";
            }
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Converts a property value to the form recorded as (part of) an entity key. Applies the same
        ///     converter step as <see cref="ToAuditValue" />, but formats the provider value through
        ///     <see cref="AuditValueFormatter.FormatKey" /> instead of <see cref="AuditValueFormatter.Format" />
        ///     so a <c>byte[]</c>-typed key (for example a converted <see cref="Guid" /> key) is recorded as
        ///     hex rather than collapsing to the same size marker for every entity.
        /// </summary>
        /// <param name="property">The EF Core property metadata.</param>
        /// <param name="value">The property value.</param>
        /// <returns>
        ///     The recorded key value, or <c>null</c> when the value is <c>null</c>.
        /// </returns>
        /// =================================================================================================
        internal static string ToAuditKeyValue(object property, object value)
        {
            if (value.IsNull())
                return null;

            var clrType = value.GetType();
            if (clrType.IsEnum)
                return value.ToString();

            var converter = PropertyMetadataHelper.GetValueConverter(property);
            if (converter.IsNull())
                return AuditValueFormatter.FormatKey(value);

            try
            {
                var providerValue = PropertyMetadataHelper.ConvertToProvider(converter, value);

                return AuditValueFormatter.FormatKey(providerValue);
            }
            catch (Exception ex)
            {
                return "[unrecordable: converter threw " + ex.GetType().Name + "]";
            }
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Gets primary key value. Exposed to the interceptor so the key can be re-read after
        ///     write action/operation completes, when a store-generated key is no longer temporary.
        /// </summary>
        /// <param name="entry">The entry.</param>
        /// <returns>
        ///     The primary key value.
        /// </returns>
        /// =================================================================================================
        internal static string GetPrimaryKeyValue(EntityEntry entry)
        {
            var keyProperties = entry.Metadata.FindPrimaryKey()?.Properties;
            if (keyProperties.IsNullOrEmptyEnumerable())
                return null;

            if (keyProperties!.Count == 1)
            {
                var keyProperty = keyProperties[0];
                var value = entry.Property(PropertyMetadataHelper.GetName(keyProperty)).CurrentValue;

                return ToAuditKeyValue(keyProperty, value);
            }

            // Composite key
            var parts = new List<string>(keyProperties.Count);
            for (var i = 0; i < keyProperties.Count; i++)
            {
                var keyProperty = keyProperties[i];
                var value = entry.Property(PropertyMetadataHelper.GetName(keyProperty)).CurrentValue;
                parts.Add(ToAuditKeyValue(keyProperty, value) ?? "null");
            }

            return parts.ListToString(",");
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Gets the names of the properties that currently hold an EF Core temporary value.
        /// </summary>
        /// <param name="entry">The entry.</param>
        /// <returns>
        ///     The temporary property names, or <c>null</c> when the entry has none.
        /// </returns>
        /// =================================================================================================
        internal static IList<string> GetTemporaryPropertyNames(EntityEntry entry)
        {
            List<string> names = null;

            foreach (var property in entry.Properties.NotNull())
            {
                if (property.IsTemporary.IsFalse())
                    continue;

                names = names ?? new List<string>(1);
                names.Add(PropertyMetadataHelper.GetName(property.Metadata));
            }

            return names;
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