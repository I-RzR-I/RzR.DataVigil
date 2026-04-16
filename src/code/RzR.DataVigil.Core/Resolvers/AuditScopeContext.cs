// ***********************************************************************
//  Assembly         : RzR.DataVigil.Core
//  Author           : RzR
//  Created On       : 2026-04-10 23:04
// 
//  Last Modified By : RzR
//  Last Modified On : 2026-04-14 20:11
// ***********************************************************************
//  <copyright file="AuditScopeContext.cs" company="RzR SOFT & TECH">
//   Copyright © RzR. All rights reserved.
//  </copyright>
// 
//  <summary>
//  </summary>
// ***********************************************************************

#region U S A G E S

using AggregatedGenericResultMessage;
using AggregatedGenericResultMessage.Abstractions;
using RzR.DataVigil.Abstractions.Models.Identity;
using RzR.DataVigil.Abstractions.Services;

#endregion

namespace RzR.DataVigil.Core.Resolvers
{
    /// -------------------------------------------------------------------------------------------------
    /// <summary>
    ///     Scoped context that allows manually setting the audit user in console/worker/test
    ///     scenarios.
    /// </summary>
    /// <seealso cref="T:RzR.DataVigil.Abstractions.Services.IAuditScopeContext"/>
    /// =================================================================================================
    public class AuditScopeContext : IAuditScopeContext
    {
        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     The current user.
        /// </summary>
        /// =================================================================================================
        private AuditUserInfo _currentUser;

        /// <inheritdoc/>
        public IResult SetUser(AuditUserInfo user)
        {
            _currentUser = user;

            return Result.Success();
        }

        /// <inheritdoc/>
        public IResult<AuditUserInfo> GetCurrentUser()
        {
            return Result<AuditUserInfo>.Success(_currentUser);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            _currentUser = null;
        }
    }
}