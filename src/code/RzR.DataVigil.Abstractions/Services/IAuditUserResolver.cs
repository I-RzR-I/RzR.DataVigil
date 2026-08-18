// ***********************************************************************
//  Assembly         : RzR.DataVigil.Abstractions
//  Author           : RzR
//  Created On       : 2026-04-10 22:04
// 
//  Last Modified By : RzR
//  Last Modified On : 2026-08-19 01:40
// ***********************************************************************
//  <copyright file="IAuditUserResolver.cs" company="RzR SOFT & TECH">
//   Copyright © RzR. All rights reserved.
//  </copyright>
// 
//  <summary>
//  </summary>
// ***********************************************************************

#region U S A G E S

using RzR.DataVigil.Abstractions.Models.Identity;
using RzR.ResultMessage.Abstractions;

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
        ///     Implementations should also set
        ///     <see cref="P:RzR.DataVigil.Abstractions.Models.Identity.AuditUserInfo.Source"/> on the
        ///     returned user to record how that identity was determined; the pipeline persists it
        ///     alongside the audit transaction so an auditor can tell a genuine attribution apart
        ///     from a failed one. Leaving it unset records
        ///     <see cref="F:RzR.DataVigil.Abstractions.Enums.AuditUserSource.Unspecified"/>.
        /// </summary>
        /// <returns>
        ///     An IResult&lt;AuditUserInfo&gt;. Return a success result carrying a null response for a
        ///     genuinely anonymous action, and a failure result only when resolution actually broke
        ///     down - the pipeline records those as
        ///     <see cref="F:RzR.DataVigil.Abstractions.Enums.AuditUserSource.Anonymous"/> and
        ///     <see cref="F:RzR.DataVigil.Abstractions.Enums.AuditUserSource.Unresolved"/>
        ///     respectively, overriding any Source stamped on the response. Never return a bare null.
        /// </returns>
        /// =================================================================================================
        IResult<AuditUserInfo> Resolve();
    }
}