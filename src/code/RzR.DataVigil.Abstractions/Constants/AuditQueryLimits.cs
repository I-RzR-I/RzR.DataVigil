// ***********************************************************************
//  Assembly         : RzR.DataVigil.Abstractions
//  Author           : RzR
//  Created On       : 2026-08-29 10:15
//
//  Last Modified By : RzR
//  Last Modified On : 2026-08-29 10:15
// ***********************************************************************
//  <copyright file="AuditQueryLimits.cs" company="RzR SOFT & TECH">
//   Copyright © RzR. All rights reserved.
//  </copyright>
//
//  <summary>
//  </summary>
// ***********************************************************************

namespace RzR.DataVigil.Abstractions.Constants
{
    /// -------------------------------------------------------------------------------------------------
    /// <summary>
    ///     Paging bounds every audit store enforces on the paging values carried by an
    ///     <see cref="T:RzR.DataVigil.Abstractions.Models.Query.AuditTransactionQuery"/>, so the same
    ///     request produces the same page whichever storage backend serves it.
    /// </summary>
    /// =================================================================================================
    public static class AuditQueryLimits
    {
        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     (Immutable) the page size substituted for a negative requested page size.
        /// </summary>
        /// =================================================================================================
        public static readonly int DefaultTake = 10;

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     (Immutable) the largest page size a store will serve. A larger request is capped, never
        ///     rejected, and the remaining records stay reachable through the offset.
        /// </summary>
        /// =================================================================================================
        public static readonly int MaxTake = 500;

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     (Immutable) the smallest offset a store will accept.
        /// </summary>
        /// =================================================================================================
        public static readonly int MinSkip = 0;
    }
}
