// ***********************************************************************
//  Assembly         : RzR.DataVigil.Abstractions
//  Author           : RzR
//  Created On       : 2026-04-10 22:04
// 
//  Last Modified By : RzR
//  Last Modified On : 2026-04-11 03:22
// ***********************************************************************
//  <copyright file="IAuditStore.cs" company="RzR SOFT & TECH">
//   Copyright © RzR. All rights reserved.
//  </copyright>
// 
//  <summary>
//  </summary>
// ***********************************************************************

#region U S A G E S

using AggregatedGenericResultMessage.Abstractions;
using RzR.DataVigil.Abstractions.Models.Entries;
using RzR.DataVigil.Abstractions.Models.Gdpr;
using RzR.DataVigil.Abstractions.Models.Query;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

#endregion

namespace RzR.DataVigil.Abstractions.Services
{
    /// -------------------------------------------------------------------------------------------------
    /// <summary>
    ///     Abstraction for audit transaction persistence and retrieval.
    /// </summary>
    /// =================================================================================================
    public interface IAuditStore
    {
        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Persists an audit transaction (with its entries) to the configured storage backend.
        /// </summary>
        /// <param name="transaction">Audit transaction to save.</param>
        /// <param name="cancellationToken">(Optional) Cancellation token.</param>
        /// <returns>
        ///     The save.
        /// </returns>
        /// =================================================================================================
        Task<IResult> SaveAsync(AuditTransaction transaction, CancellationToken cancellationToken = default);

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Queries the audit store.
        /// </summary>
        /// <param name="filters">The filters.</param>
        /// <param name="gdprRetrievalContext">(Optional) Context for the gdpr retrieval.</param>
        /// <param name="cancellationToken">(Optional) Cancellation token.</param>
        /// <returns>
        ///     The query.
        /// </returns>
        /// =================================================================================================
        Task<IResult<IEnumerable<AuditTransaction>>> QueryAsync(AuditTransactionQuery filters, GdprRetrievalContext gdprRetrievalContext = null,
            CancellationToken cancellationToken = default);

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     GDPR right-to-erasure: anonymize all transactions for a given user.
        /// </summary>
        /// <param name="userId">Identifier for the user.</param>
        /// <param name="cancellationToken">(Optional) Cancellation token.</param>
        /// <returns>
        ///     The anonymize by user.
        /// </returns>
        /// =================================================================================================
        Task<IResult> AnonymizeByUserAsync(string userId, CancellationToken cancellationToken = default);

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Retention policy: purge transactions older than the given date.
        /// </summary>
        /// <param name="before">The before.</param>
        /// <param name="cancellationToken">(Optional) Cancellation token.</param>
        /// <returns>
        ///     The purge before.
        /// </returns>
        /// =================================================================================================
        Task<IResult> PurgeBeforeAsync(DateTimeOffset before, CancellationToken cancellationToken = default);
    }
}