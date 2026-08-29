// ***********************************************************************
//  Assembly         : RzR.DataVigil.Abstractions
//  Author           : RzR
//  Created On       : 2026-08-29 10:15
// 
//  Last Modified By : RzR
//  Last Modified On : 2026-08-29 10:15
// ***********************************************************************
//  <copyright file="AuditResultCodes.cs" company="RzR SOFT & TECH">
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
    ///     Stable message keys the library puts on the results it returns, so a caller can tell one
    ///     failure apart from another without matching on the failure text, which is culture dependent
    ///     and comes from an exception the library does not own.
    /// </summary>
    /// =================================================================================================
    public static class AuditResultCodes
    {
        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Message key reporting that the audit work was abandoned because the caller's own
        ///     cancellation token was canceled. This is an expected outcome of a shutdown or an aborted
        ///     request, not a defect, so a caller is meant to demote its own report of it rather than
        ///     raise it as a failure.
        /// </summary>
        /// =================================================================================================
        public const string OperationCanceled = "DATAVIGIL.AUDIT.CANCELED";
    }
}