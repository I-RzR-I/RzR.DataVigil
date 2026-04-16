// ***********************************************************************
//  Assembly         : RzR.DataVigil.Abstractions
//  Author           : RzR
//  Created On       : 2026-04-10 22:04
// 
//  Last Modified By : RzR
//  Last Modified On : 2026-04-11 03:22
// ***********************************************************************
//  <copyright file="IAuditSourceResolver.cs" company="RzR SOFT & TECH">
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
    ///     Resolves the source/origin of the audited action (e.g. "WebApi", "WorkerService",
    ///     "Console").
    /// </summary>
    /// =================================================================================================
    public interface IAuditSourceResolver
    {
        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Resolves and returns the source identifier for the current application host.
        /// </summary>
        /// <returns>
        ///     An IResult&lt;string&gt;
        /// </returns>
        /// =================================================================================================
        IResult<string> Resolve();
    }
}