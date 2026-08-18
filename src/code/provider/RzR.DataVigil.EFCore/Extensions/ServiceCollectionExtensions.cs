// ***********************************************************************
//  Assembly         : RzR.DataVigil.EFCore
//  Author           : RzR
//  Created On       : 2026-04-10 23:04
// 
//  Last Modified By : RzR
//  Last Modified On : 2026-04-14 18:10
// ***********************************************************************
//  <copyright file="ServiceCollectionExtensions.cs" company="RzR SOFT & TECH">
//   Copyright © RzR. All rights reserved.
//  </copyright>
// 
//  <summary>
//  </summary>
// ***********************************************************************

#region U S A G E S

using Microsoft.Extensions.DependencyInjection;
using RzR.DataVigil.EFCore.Interceptors;

#endregion

namespace RzR.DataVigil.EFCore.Extensions
{
    /// -------------------------------------------------------------------------------------------------
    /// <summary>
    ///     Extension methods for registering EF Core audit services.
    /// </summary>
    /// =================================================================================================
    public static class ServiceCollectionExtensions
    {
        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Registers EF Core audit interceptors in DI. May be called before or after
        ///     AddAuditTrail() — it registers only concrete types, so it is order-independent.
        /// </summary>
        /// <param name="services">The services to act on.</param>
        /// <returns>
        ///     An IServiceCollection.
        /// </returns>
        /// =================================================================================================
        public static IServiceCollection AddAuditTrailEfCore(this IServiceCollection services)
        {
            services.AddScoped<AuditSaveChangesInterceptor>();
            services.AddScoped<AuditCommandInterceptor>();
            services.AddScoped<AuditReadService>();

            return services;
        }
    }
}