// ***********************************************************************
//  Assembly         : RzR.DataVigil.Core
//  Author           : RzR
//  Created On       : 2026-08-28 20:40
//
//  Last Modified By : RzR
//  Last Modified On : 2026-08-29 00:15
// ***********************************************************************
//  <copyright file="ActivityTraceHelper.cs" company="RzR SOFT & TECH">
//   Copyright © RzR. All rights reserved.
//  </copyright>
//
//  <summary>
//  </summary>
// ***********************************************************************

#region U S A G E S

using System.Diagnostics;
using RzR.Extensions.Domain.Primitives;

#endregion

namespace RzR.DataVigil.Core.Helpers
{
    /// -------------------------------------------------------------------------------------------------
    /// <summary>
    ///     Shared access to the ambient W3C distributed trace id, used by every
    ///     <see cref="T:RzR.DataVigil.Abstractions.Services.IAuditCorrelationProvider"/> implementation.
    /// </summary>
    /// =================================================================================================
    internal static class ActivityTraceHelper
    {
        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     The current W3C distributed trace id, or null when there is no usable trace context.
        /// </summary>
        /// <returns>
        ///     A 32-character lowercase hex trace id, or null.
        /// </returns>
        /// =================================================================================================
        internal static string GetW3CTraceId()
        {
            var current = Activity.Current;

            return current.IsNotNull()
                   && current.IdFormat == ActivityIdFormat.W3C
                   && current.TraceId != default
                ? current.TraceId.ToHexString()
                : null;
        }
    }
}
