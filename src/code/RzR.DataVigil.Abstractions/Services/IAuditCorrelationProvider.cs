// ***********************************************************************
//  Assembly         : RzR.DataVigil.Abstractions
//  Author           : RzR
//  Created On       : 2026-04-10 22:04
// 
//  Last Modified By : RzR
//  Last Modified On : 2026-04-11 03:22
// ***********************************************************************
//  <copyright file="IAuditCorrelationProvider.cs" company="RzR SOFT & TECH">
//   Copyright © RzR. All rights reserved.
//  </copyright>
// 
//  <summary>
//  </summary>
// ***********************************************************************

#region U S A G E S

using AggregatedGenericResultMessage.Abstractions;

#endregion

namespace RzR.DataVigil.Abstractions.Services
{
    /// -------------------------------------------------------------------------------------------------
    /// <summary>
    ///     Provides correlation and trace identifiers for audit entries.
    /// </summary>
    /// =================================================================================================
    public interface IAuditCorrelationProvider
    {
        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Returns the correlation identifier for the current operation scope.
        /// </summary>
        /// <returns>
        ///     The correlation identifier.
        /// </returns>
        /// =================================================================================================
        IResult<string> GetCorrelationId();

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Returns the distributed trace identifier for the current request.
        /// </summary>
        /// <returns>
        ///     The trace identifier.
        /// </returns>
        /// =================================================================================================
        IResult<string> GetTraceId();
    }
}