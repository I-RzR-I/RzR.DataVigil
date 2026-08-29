// ***********************************************************************
//  Assembly         : RzR.DataVigil.Abstractions
//  Author           : RzR
//  Created On       : 2026-08-29 10:15
// 
//  Last Modified By : RzR
//  Last Modified On : 2026-08-29 10:15
// ***********************************************************************
//  <copyright file="AuditResultExtensions.cs" company="RzR SOFT & TECH">
//   Copyright © RzR. All rights reserved.
//  </copyright>
// 
//  <summary>
//  </summary>
// ***********************************************************************

#region U S A G E S

using System;
using RzR.DataVigil.Abstractions.Constants;
using RzR.ResultMessage.Abstractions;

#endregion

namespace RzR.DataVigil.Abstractions.Extensions
{
    /// -------------------------------------------------------------------------------------------------
    /// <summary>
    ///     Extension methods that classify the results the audit pipeline returns.
    /// </summary>
    /// =================================================================================================
    public static class AuditResultExtensions
    {
        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Returns <c>true</c> when the result reports that the audit work was abandoned because the
        ///     caller's own cancellation token was canceled. A caller uses this to keep a shutdown or an
        ///     aborted request out of its failure reporting, because the audit record is genuinely
        ///     missing but nothing went wrong.
        /// </summary>
        /// <param name="result">The result to act on, may be null.</param>
        /// <returns>
        ///     True when the result carries the cancellation message key, false in every other case.
        /// </returns>
        /// =================================================================================================
        public static bool IsAuditCanceled(this IResult result)
        {
            if (result == null || result.Messages == null)
                return false;

            foreach (var message in result.Messages)
            {
                if (message != null &&
                    string.Equals(message.Key, AuditResultCodes.OperationCanceled, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }
    }
}