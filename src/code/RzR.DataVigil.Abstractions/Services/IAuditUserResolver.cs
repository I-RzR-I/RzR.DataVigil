// ***********************************************************************
//  Assembly         : RzR.DataVigil.Abstractions
//  Author           : RzR
//  Created On       : 2026-04-10 22:04
// 
//  Last Modified By : RzR
//  Last Modified On : 2026-04-11 03:22
// ***********************************************************************
//  <copyright file="IAuditUserResolver.cs" company="RzR SOFT & TECH">
//   Copyright © RzR. All rights reserved.
//  </copyright>
// 
//  <summary>
//  </summary>
// ***********************************************************************

#region U S A G E S

using AggregatedGenericResultMessage.Abstractions;
using RzR.DataVigil.Abstractions.Models.Identity;

#endregion

namespace RzR.DataVigil.Abstractions.Services
{
    /// -------------------------------------------------------------------------------------------------
    /// <summary>
    ///     Resolves the current user performing the audited action. Works in any host: Web API,
    ///     Worker Service, Console, WinForms.
    /// </summary>
    /// =================================================================================================
    public interface IAuditUserResolver
    {
        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Resolves and returns information about the currently authenticated user.
        /// </summary>
        /// <returns>
        ///     An IResult&lt;AuditUserInfo&gt;
        /// </returns>
        /// =================================================================================================
        IResult<AuditUserInfo> Resolve();
    }
}