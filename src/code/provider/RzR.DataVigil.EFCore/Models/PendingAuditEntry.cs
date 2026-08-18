// ***********************************************************************
//  Assembly          : RzR.DataVigil.EFCore
//  Author            : RzR
//  Created           : 18-08-2026 23:08
// 
//  Last Modified By : RzR
//  Last Modified On : 19-08-2026 00:00
//  ***********************************************************************
//  <copyright file="PendingAuditEntry.cs" company="RzR SOFT & TECH">
//      Copyright (c) RzR. All rights reserved.
//  </copyright>
//  <contact>
//      https://iamrzr.dev/contact
//  </contact>
//  <summary></summary>
//  ***********************************************************************

#region U S I N G

using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using RzR.DataVigil.Abstractions.Models.Entries;

#endregion

namespace RzR.DataVigil.EFCore.Models
{
    /// -------------------------------------------------------------------------------------------------
    /// <summary>
    ///     Correlates a built <see cref="AuditEntry" /> back to the tracked
    ///     <see cref="EntityEntry" /> it came from, so that store-generated values can be
    ///     re-read once the INSERT has completed.
    /// </summary>
    /// =================================================================================================
    internal sealed class PendingAuditEntry
    {
        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Initializes a new instance of the <see cref="PendingAuditEntry" /> class.
        /// </summary>
        /// <param name="auditEntry">The audit entry built during the collect phase.</param>
        /// <param name="entityEntry">The tracked entity entry the audit entry was built from.</param>
        /// <param name="temporaryPropertyNames">
        ///     Names of the properties that held an EF temporary value at collect time.
        /// </param>
        /// =================================================================================================
        public PendingAuditEntry(AuditEntry auditEntry, EntityEntry entityEntry,
            IEnumerable<string> temporaryPropertyNames)
        {
            AuditEntry = auditEntry;
            EntityEntry = entityEntry;
            TemporaryPropertyNames = temporaryPropertyNames;
        }

        /// <summary>
        ///     Gets the audit entry to patch.
        /// </summary>
        /// <value>
        ///     The audit entry.
        /// </value>
        public AuditEntry AuditEntry { get; }

        /// <summary>
        ///     Gets the tracked entity entry to re-read values from.
        /// </summary>
        /// <value>
        ///     The entity entry.
        /// </value>
        public EntityEntry EntityEntry { get; }

        /// <summary>
        ///     Gets the property names that carried a temporary value at collect time. Captured during
        ///     collect because <c>PropertyEntry.IsTemporary</c> is cleared by EF once the 
        ///     store-generated value is propagated back.
        /// </summary>
        /// <value>
        ///     A list of names of the temporary properties.
        /// </value>
        public IEnumerable<string> TemporaryPropertyNames { get; }
    }
}