// ***********************************************************************
//  Assembly         : RzR.DataVigil.Abstractions
//  Author           : RzR
//  Created On       : 2026-04-15 01:04
// 
//  Last Modified By : RzR
//  Last Modified On : 2026-08-29 00:00
// ***********************************************************************
//  <copyright file="AuditTransactionQuery.cs" company="RzR SOFT & TECH">
//   Copyright © RzR. All rights reserved.
//  </copyright>
// 
//  <summary>
//  </summary>
// ***********************************************************************

#region U S A G E S

using System;
using RzR.DataVigil.Abstractions.Enums;

#endregion

namespace RzR.DataVigil.Abstractions.Models.Query
{
    /// -------------------------------------------------------------------------------------------------
    /// <summary>
    ///     Query parameters for filtering and paginating audit transaction results.
    /// </summary>
    /// <remarks>
    ///     Filter properties are added additively over time, so a store applies only the filters it
    ///     knows about. A filter left at its default (<see langword="null"/>, or null-or-whitespace for
    ///     the string filters) is not applied at all, which means a default instance produces the same
    ///     unfiltered result set as before any filter existed. Supplied filters combine with AND only.
    /// </remarks>
    /// =================================================================================================
    public class AuditTransactionQuery
    {
        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Gets or sets the number of records to skip (offset). Default is 0.
        /// </summary>
        /// =================================================================================================
        public int Skip { get; set; } = 0;

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Gets or sets the maximum number of records to return. Default is 10.
        /// </summary>
        /// =================================================================================================
        public int Take { get; set; } = 10;

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Gets or sets the inclusive lower bound of the transaction timestamp range.
        ///     <see langword="null"/> leaves the range open at the lower end.
        /// </summary>
        /// =================================================================================================
        public DateTimeOffset? FromUtc { get; set; }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Gets or sets the exclusive upper bound of the transaction timestamp range.
        ///     <see langword="null"/> leaves the range open at the upper end.
        /// </summary>
        /// =================================================================================================
        public DateTimeOffset? ToUtc { get; set; }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Gets or sets the actor identifier to match. Null or whitespace applies no filter.
        /// </summary>
        /// =================================================================================================
        public string UserId { get; set; }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Gets or sets the correlation identifier to match. Null or whitespace applies no filter.
        /// </summary>
        /// =================================================================================================
        public string CorrelationId { get; set; }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Gets or sets the GDPR processing state to match. <see langword="null"/> applies no filter.
        /// </summary>
        /// =================================================================================================
        public GdprStorageState? GdprState { get; set; }
    }
}
