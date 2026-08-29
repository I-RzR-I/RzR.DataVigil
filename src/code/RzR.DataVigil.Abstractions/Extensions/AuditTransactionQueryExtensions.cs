// ***********************************************************************
//  Assembly         : RzR.DataVigil.Abstractions
//  Author           : RzR
//  Created On       : 2026-08-29 10:15
//
//  Last Modified By : RzR
//  Last Modified On : 2026-08-29 10:15
// ***********************************************************************
//  <copyright file="AuditTransactionQueryExtensions.cs" company="RzR SOFT & TECH">
//   Copyright © RzR. All rights reserved.
//  </copyright>
//
//  <summary>
//  </summary>
// ***********************************************************************

#region U S A G E S

using System;
using RzR.DataVigil.Abstractions.Constants;
using RzR.DataVigil.Abstractions.Models.Query;

#endregion

namespace RzR.DataVigil.Abstractions.Extensions
{
    /// -------------------------------------------------------------------------------------------------
    /// <summary>
    ///     Extension methods for <see cref="AuditTransactionQuery"/>.
    /// </summary>
    /// =================================================================================================
    public static class AuditTransactionQueryExtensions
    {
        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Resolves the paging values a store will actually execute with from the paging values the
        ///     caller requested, bounded by <see cref="AuditQueryLimits"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">
        ///     Thrown when <paramref name="filters"/> is null.
        /// </exception>
        /// <param name="filters">The requested filters, including the requested paging values.</param>
        /// <param name="skip">[out] The offset to execute with.</param>
        /// <param name="take">[out] The page size to execute with; zero means no record was asked for.</param>
        /// <param name="pagingWasNormalized">
        ///     [out] True when a negative offset or a negative page size was replaced by a usable value,
        ///     which is a caller mistake worth a debug trace and nothing more.
        /// </param>
        /// <param name="takeWasCapped">
        ///     [out] True when the requested page size exceeded <see cref="AuditQueryLimits.MaxTake"/> and
        ///     was capped. This one is worth a warning: an auditor who asked for more records than a store
        ///     will serve must not read the short page as "no further records exist".
        /// </param>
        /// =================================================================================================
        public static void GetEffectivePaging(this AuditTransactionQuery filters, out int skip, out int take,
            out bool pagingWasNormalized, out bool takeWasCapped)
        {
            if (filters == null)
                throw new ArgumentNullException(nameof(filters));

            skip = filters.Skip;
            take = filters.Take;
            pagingWasNormalized = false;
            takeWasCapped = false;

            if (skip < AuditQueryLimits.MinSkip)
            {
                skip = AuditQueryLimits.MinSkip;
                pagingWasNormalized = true;
            }

            if (take < 0)
            {
                take = AuditQueryLimits.DefaultTake;
                pagingWasNormalized = true;
            }
            else if (take > AuditQueryLimits.MaxTake)
            {
                take = AuditQueryLimits.MaxTake;
                takeWasCapped = true;
            }
        }
    }
}
