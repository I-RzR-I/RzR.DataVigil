// ***********************************************************************
//  Assembly         : RzR.DataVigil.Abstractions
//  Author           : RzR
//  Created On       : 2026-04-10 22:04
// 
//  Last Modified By : RzR
//  Last Modified On : 2026-04-11 03:22
// ***********************************************************************
//  <copyright file="IAuditScopeContext.cs" company="RzR SOFT & TECH">
//   Copyright © RzR. All rights reserved.
//  </copyright>
// 
//  <summary>
//  </summary>
// ***********************************************************************

#region U S A G E S

using System;
using RzR.DataVigil.Abstractions.Models.Identity;
using RzR.ResultMessage.Abstractions;

#endregion

namespace RzR.DataVigil.Abstractions.Services
{
    /// -------------------------------------------------------------------------------------------------
    /// <summary>
    ///     Scoped context that allows manually setting the audit user in console/worker/test
    ///     scenarios where there is no HttpContext.
    /// </summary>
    /// =================================================================================================
    public interface IAuditScopeContext : IDisposable
    {
        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Sets the user identity for the current audit scope.
        /// </summary>
        /// <param name="user">User information to associate with audit entries in this scope.</param>
        /// <returns>
        ///     An IResult.
        /// </returns>
        /// =================================================================================================
        IResult SetUser(AuditUserInfo user);

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Returns the user identity previously set in this scope, or null if not set.
        /// </summary>
        /// <returns>
        ///     The current user.
        /// </returns>
        /// =================================================================================================
        IResult<AuditUserInfo> GetCurrentUser();

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Sets the correlation id for the current scope, overriding whatever the ambient environment
        ///     would supply. Intended for non-HTTP hosts - a worker can pass the correlation id carried on
        ///     the queue message it is handling.
        /// </summary>
        /// <param name="correlationId">The correlation id, or null to clear it.</param>
        /// <returns>
        ///     An IResult.
        /// </returns>
        /// =================================================================================================
        IResult SetCorrelationId(string correlationId);

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Gets the correlation id set for the current scope, if any.
        /// </summary>
        /// <returns>
        ///     An IResult&lt;string&gt; whose response is null when nothing was set.
        /// </returns>
        /// =================================================================================================
        IResult<string> GetCurrentCorrelationId();
    }
}