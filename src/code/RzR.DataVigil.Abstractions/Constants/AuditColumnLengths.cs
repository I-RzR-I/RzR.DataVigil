// ***********************************************************************
//  Assembly         : RzR.DataVigil.Abstractions
//  Author           : RzR
//  Created On       : 2026-08-28 21:10
//
//  Last Modified By : RzR
//  Last Modified On : 2026-08-29 21:10
// ***********************************************************************
//  <copyright file="AuditColumnLengths.cs" company="RzR SOFT & TECH">
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
    ///     Storage column lengths that both the persistence mapping and the validation performed before
    ///     persistence must agree on.
    /// </summary>
    /// =================================================================================================
    public static class AuditColumnLengths
    {
        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     (Immutable) the maximum length of the audit correlation id column.
        /// </summary>
        /// =================================================================================================
        public static readonly int CorrelationId = 256;
    }
}
