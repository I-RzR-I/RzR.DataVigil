// ***********************************************************************
//  Assembly          : RzR.DataVigil.EFCore
//  Author            : RzR
//  Created           : 18-08-2026 23:08
// 
//  Last Modified By : RzR
//  Last Modified On : 18-08-2026 23:58
//  ***********************************************************************
//  <copyright file="PendingAuditTransaction.cs" company="RzR SOFT & TECH">
//      Copyright (c) RzR. All rights reserved.
//  </copyright>
//  <contact>
//      https://iamrzr.dev/contact
//  </contact>
//  <summary></summary>
//  ***********************************************************************

#region U S I N G

using System.Collections.Generic;
using RzR.DataVigil.Abstractions.Models.Entries;

#endregion

namespace RzR.DataVigil.EFCore.Models
{
    /// -------------------------------------------------------------------------------------------------
    /// <summary>
    ///     An audit transaction collected during <c>SavingChanges</c> and held until
    ///     <c>SavedChanges</c>, together with the entries whose values must be re-read
    ///     after write action has completed.
    /// </summary>
    /// =================================================================================================
    internal sealed class PendingAuditTransaction
    {
        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Initializes a new instance of the <see cref="PendingAuditTransaction" /> class.
        /// </summary>
        /// <param name="transaction">The collected transaction.</param>
        /// <param name="pendingEntries">
        ///     Entries requiring a post-save value re-read. May be empty.
        /// </param>
        /// =================================================================================================
        public PendingAuditTransaction(AuditTransaction transaction,
            IEnumerable<PendingAuditEntry> pendingEntries)
        {
            Transaction = transaction;
            PendingEntries = pendingEntries;
        }

        /// <summary>
        ///     Gets the collected audit transaction.
        /// </summary>
        /// <value>
        ///     The transaction.
        /// </value>
        public AuditTransaction Transaction { get; }

        /// <summary>
        ///     Gets the entries requiring a post-save value re-read.
        /// </summary>
        /// <value>
        ///     The pending entries.
        /// </value>
        public IEnumerable<PendingAuditEntry> PendingEntries { get; }
    }
}