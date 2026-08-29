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

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     (Immutable) the maximum length of the audit user id column.
        /// </summary>
        /// =================================================================================================
        public static readonly int UserId = 256;

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     (Immutable) the maximum length of the audit username column.
        /// </summary>
        /// =================================================================================================
        public static readonly int UserName = 256;

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     (Immutable) the maximum length of the audit ip address column.
        /// </summary>
        /// =================================================================================================
        public static readonly int IpAddress = 64;

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     (Immutable) the maximum length of the audit source column.
        /// </summary>
        /// =================================================================================================
        public static readonly int Source = 512;

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     (Immutable) the maximum length of the audit trace id column.
        /// </summary>
        /// =================================================================================================
        public static readonly int TraceId = 256;
    }
}
