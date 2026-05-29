// ***********************************************************************
//  Assembly         : RzR.DataVigil.Core
//  Author           : RzR
//  Created On       : 2026-04-10 23:04
// 
//  Last Modified By : RzR
//  Last Modified On : 2026-04-14 20:12
// ***********************************************************************
//  <copyright file="DefaultUserResolver.cs" company="RzR SOFT & TECH">
//   Copyright © RzR. All rights reserved.
//  </copyright>
// 
//  <summary>
//  </summary>
// ***********************************************************************

#region U S A G E S

using System.Threading;
using RzR.DataVigil.Abstractions.Models.Identity;
using RzR.DataVigil.Abstractions.Services;
using RzR.Extensions.Domain.Primitives;
using RzR.ResultMessage;
using RzR.ResultMessage.Abstractions;

#endregion

namespace RzR.DataVigil.Core.Resolvers
{
    /// -------------------------------------------------------------------------------------------------
    /// <summary>
    ///     Default user resolver with fallback chain:
    ///     1. IAuditScopeContext (manually set in worker/console/test)
    ///     2. Thread.CurrentPrincipal
    ///     3. null (anonymous)
    /// </summary>
    /// <seealso cref="T:RzR.DataVigil.Abstractions.Services.IAuditUserResolver"/>
    /// =================================================================================================
    public class DefaultUserResolver : IAuditUserResolver
    {
        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     (Immutable) context for the scope.
        /// </summary>
        /// =================================================================================================
        private readonly IAuditScopeContext _scopeContext;

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Initializes a new instance of the <see cref="DefaultUserResolver"/> class.
        /// </summary>
        /// <param name="scopeContext">Context for the scope.</param>
        /// =================================================================================================
        public DefaultUserResolver(IAuditScopeContext scopeContext)
        {
            _scopeContext = scopeContext;
        }

        /// <inheritdoc/>
        public IResult<AuditUserInfo> Resolve()
        {
            // 1. Check scope context (manually set)
            var scopeUser = _scopeContext.GetCurrentUser();
            if (scopeUser.IsNotNull())
                return scopeUser;

            // 2. Check Thread.CurrentPrincipal
            var principal = Thread.CurrentPrincipal;
            if (principal?.Identity?.IsAuthenticated == true)
            {
                var result = new AuditUserInfo
                {
                    UserId = principal.Identity.Name,
                    UserName = principal.Identity.Name
                };

                return Result<AuditUserInfo>.Success(result);
            }

            // 3. Anonymous
            return Result<AuditUserInfo>.Success();
        }
    }
}