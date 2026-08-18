// ***********************************************************************
//  Assembly         : RzR.DataVigil.Abstractions
//  Author           : RzR
//  Created On       : 2026-08-18 22:30
//
//  Last Modified By : RzR
//  Last Modified On : 2026-08-19 00:00
// ***********************************************************************
//  <copyright file="AuditUserSource.cs" company="RzR SOFT & TECH">
//   Copyright © RzR. All rights reserved.
//  </copyright>
//
//  <summary>
//  </summary>
// ***********************************************************************

namespace RzR.DataVigil.Abstractions.Enums
{
    /// -------------------------------------------------------------------------------------------------
    /// <summary>
    ///     Identifies how the actor recorded on an audit transaction was determined. Persisted
    ///     alongside the transaction so an auditor can distinguish "resolution genuinely found
    ///     nobody" from "attribution silently failed".
    /// </summary>
    /// =================================================================================================
    public enum AuditUserSource
    {
        /// <summary>
        ///     A resolver returned a user, but did not declare where that identity came from. This
        ///     does not mean resolution failed — that is <see cref="Unresolved"/>; the recorded
        ///     actor is real and can be trusted, only its provenance is undeclared. In practice this
        ///     marks a custom <see cref="T:RzR.DataVigil.Abstractions.Services.IAuditUserResolver"/>
        ///     that predates the
        ///     <see cref="P:RzR.DataVigil.Abstractions.Models.Identity.AuditUserInfo.Source"/>
        ///     property, or one that never sets it.
        /// </summary>
        Unspecified = 0,

        /// <summary>
        ///     No <see cref="T:RzR.DataVigil.Abstractions.Services.IAuditUserResolver"/> could be
        ///     consulted, or the resolver returned a failed result. Attribution failed — the absence
        ///     of a user on this record does not mean the action was anonymous, only that identity
        ///     resolution broke down.
        /// </summary>
        Unresolved = 1,

        /// <summary>
        ///     The resolver ran successfully and determined there is genuinely no user for this
        ///     action (e.g. an unauthenticated request, a system/background job with no principal).
        /// </summary>
        Anonymous = 2,

        /// <summary>
        ///     The user was taken from an
        ///     <see cref="T:RzR.DataVigil.Abstractions.Services.IAuditScopeContext"/> manual override
        ///     (e.g. a worker/console host or a test explicitly setting the acting user).
        /// </summary>
        ScopeContext = 3,

        /// <summary>
        ///     The user was extracted from the ASP.NET Core <c>HttpContext</c>'s authenticated
        ///     principal.
        /// </summary>
        HttpContext = 4,

        /// <summary>
        ///     The user was extracted from <see cref="System.Threading.Thread.CurrentPrincipal"/>.
        /// </summary>
        ThreadPrincipal = 5
    }
}
