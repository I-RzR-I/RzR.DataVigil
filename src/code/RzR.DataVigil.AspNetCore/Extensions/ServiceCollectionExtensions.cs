// ***********************************************************************
//  Assembly         : RzR.DataVigil.AspNetCore
//  Author           : RzR
//  Created On       : 2026-04-11 00:04
// 
//  Last Modified By : RzR
//  Last Modified On : 2026-04-14 20:14
// ***********************************************************************
//  <copyright file="ServiceCollectionExtensions.cs" company="RzR SOFT & TECH">
//   Copyright © RzR. All rights reserved.
//  </copyright>
// 
//  <summary>
//  </summary>
// ***********************************************************************

#region U S A G E S

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RzR.DataVigil.Abstractions.Services;
using RzR.DataVigil.AspNetCore.Resolvers;

#endregion

namespace RzR.DataVigil.AspNetCore.Extensions
{
    /// -------------------------------------------------------------------------------------------------
    /// <summary>
    ///     Extension methods for registering ASP.NET Core audit resolvers.
    /// </summary>
    /// =================================================================================================
    public static class ServiceCollectionExtensions
    {
        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Registers ASP.NET Core-specific audit resolvers: user resolver from HttpContext,
        ///     correlation from HTTP headers. Call after AddAuditTrail().
        /// </summary>
        /// <param name="services">The services to act on.</param>
        /// <returns>
        ///     An IServiceCollection.
        /// </returns>
        /// =================================================================================================
        public static IServiceCollection AddAuditTrailAspNetCore(this IServiceCollection services)
        {
            services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            services.AddScoped<IAuditUserResolver, AspNetCoreUserResolver>();
            services.AddScoped<IAuditCorrelationProvider, AspNetCoreCorrelationProvider>();

            return services;
        }
    }
}