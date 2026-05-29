// ***********************************************************************
//  Assembly         : RzR.DataVigil.AspNetCore
//  Author           : RzR
//  Created On       : 2026-04-11 00:04
// 
//  Last Modified By : RzR
//  Last Modified On : 2026-04-14 20:15
// ***********************************************************************
//  <copyright file="AspNetCoreUserResolver.cs" company="RzR SOFT & TECH">
//   Copyright © RzR. All rights reserved.
//  </copyright>
// 
//  <summary>
//  </summary>
// ***********************************************************************

#region U S A G E S

using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using RzR.DataVigil.Abstractions.Models.Identity;
using RzR.DataVigil.Abstractions.Services;
using RzR.Extensions.Domain.Primitives;
using RzR.ResultMessage;
using RzR.ResultMessage.Abstractions;

#endregion

namespace RzR.DataVigil.AspNetCore.Resolvers
{
    /// -------------------------------------------------------------------------------------------------
    /// <summary>
    ///     Resolves the current user from HttpContext (ASP.NET Core). Extracts UserId, UserName,
    ///     IpAddress, Roles, and Claims. Falls back to IAuditScopeContext for worker/test overrides.
    /// 
    /// </summary>
    /// <seealso cref="T:RzR.DataVigil.Abstractions.Services.IAuditUserResolver"/>
    /// =================================================================================================
    public class AspNetCoreUserResolver : IAuditUserResolver
    {
        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     (Immutable) the HTTP context accessor.
        /// </summary>
        /// =================================================================================================
        private readonly IHttpContextAccessor _httpContextAccessor;

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     (Immutable) context for the scope.
        /// </summary>
        /// =================================================================================================
        private readonly IAuditScopeContext _scopeContext;

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Initializes a new instance of the <see cref="AspNetCoreUserResolver"/> class.
        /// </summary>
        /// <param name="httpContextAccessor">The HTTP context accessor.</param>
        /// <param name="scopeContext">Context for the scope.</param>
        /// =================================================================================================
        public AspNetCoreUserResolver(
            IHttpContextAccessor httpContextAccessor,
            IAuditScopeContext scopeContext)
        {
            _httpContextAccessor = httpContextAccessor;
            _scopeContext = scopeContext;
        }

        /// <inheritdoc/>
        public IResult<AuditUserInfo> Resolve()
        {
            // 1. Check scope context (manually set — worker/test override)
            var scopeUser = _scopeContext.GetCurrentUser();
            if (scopeUser.IsNotNull())
                return scopeUser;

            // 2. Check HttpContext
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext?.User?.Identity?.IsAuthenticated != true)
                return null;

            var user = httpContext.User;

            var result = new AuditUserInfo
            {
                UserId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                         ?? user.FindFirst("sub")?.Value
                         ?? user.Identity.Name,
                UserName = user.Identity.Name,
                IpAddress = httpContext.Connection?.RemoteIpAddress?.ToString(),
                Roles = user.Claims
                    .Where(c => c.Type == ClaimTypes.Role || c.Type == "role")
                    .Select(c => c.Value),
                Claims = user.Claims
                    .Where(c => c.Type != ClaimTypes.Role && c.Type != "role")
                    .GroupBy(c => c.Type)
                    .ToDictionary(g => g.Key, g => g.First().Value)
            };

            return Result<AuditUserInfo>.Success(result);
        }
    }
}