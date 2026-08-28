// ***********************************************************************
//  Assembly         : RzR.DataVigil.AspNetCore
//  Author           : RzR
//  Created On       : 2026-04-11 00:04
// 
//  Last Modified By : RzR
//  Last Modified On : 2026-08-28 20:40
// ***********************************************************************
//  <copyright file="ServiceCollectionExtensions.cs" company="RzR SOFT & TECH">
//   Copyright © RzR. All rights reserved.
//  </copyright>
// 
//  <summary>
//  </summary>
// ***********************************************************************

#region U S A G E S

using System;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using RzR.DataVigil.Abstractions.Services;
using RzR.DataVigil.AspNetCore.Enrichers;
using RzR.DataVigil.AspNetCore.Hosting;
using RzR.DataVigil.AspNetCore.Resolvers;
using RzR.DataVigil.Core.Resolvers;
using RzR.Extensions.Domain.Primitives;

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
        ///     correlation from HTTP headers.
        ///     <para>
        ///     May be called before or after <c>AddAuditTrail()</c>. A resolver configured
        ///     explicitly via <c>UseUserResolver&lt;T&gt;()</c> always takes precedence over the
        ///     ASP.NET Core one.
        ///     </para>
        /// </summary>
        /// <param name="services">The services to act on.</param>
        /// <returns>
        ///     An IServiceCollection.
        /// </returns>
        /// =================================================================================================
        public static IServiceCollection AddAuditTrailAspNetCore(this IServiceCollection services)
            => AddAuditTrailAspNetCore(services, null);

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Registers the ASP.NET Core-specific audit resolvers as
        ///     <see cref="M:RzR.DataVigil.AspNetCore.Extensions.ServiceCollectionExtensions.AddAuditTrailAspNetCore(Microsoft.Extensions.DependencyInjection.IServiceCollection)"/>
        ///     does, and additionally stamps the matched route template onto each audit transaction.
        /// </summary>
        /// <param name="services">The services to act on.</param>
        /// <param name="routeTemplateAccessor">
        ///     Returns the matched route template for a context, or null when none matched.
        /// </param>
        /// <returns>
        ///     An IServiceCollection.
        /// </returns>
        /// =================================================================================================
        public static IServiceCollection AddAuditTrailAspNetCore(
            this IServiceCollection services,
            Func<HttpContext, string> routeTemplateAccessor)
        {
            services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();

            TakeOverFromDefault<IAuditUserResolver, DefaultUserResolver, AspNetCoreUserResolver>(services);
            TakeOverFromDefault<IAuditCorrelationProvider, DefaultCorrelationProvider, AspNetCoreCorrelationProvider>(services);

            services.TryAddEnumerable(
                ServiceDescriptor.Scoped<IAuditMetadataEnricher, HttpOperationMetadataEnricher>(
                    sp => new HttpOperationMetadataEnricher(
                        sp.GetRequiredService<IHttpContextAccessor>(),
                        routeTemplateAccessor,
                        sp.GetService<IAuditScopeContext>())));

            services.TryAddEnumerable(
                ServiceDescriptor.Singleton<IHostedService, AuditIdentityResolutionDiagnostic>());

            return services;
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Installs the ASP.NET Core implementation of a contract, but only when the slot is
        ///     free or still holds the built-in default.
        /// </summary>
        /// <typeparam name="TService">The contract.</typeparam>
        /// <typeparam name="TDefault">The built-in default implementation.</typeparam>
        /// <typeparam name="TAspNetCore">The ASP.NET Core implementation.</typeparam>
        /// <param name="services">The services to act on.</param>
        /// =================================================================================================
        private static void TakeOverFromDefault<TService, TDefault, TAspNetCore>(IServiceCollection services)
            where TDefault : TService
            where TAspNetCore : TService
        {
            var current = services.LastOrDefault(d => d.ServiceType == typeof(TService));

            if (current.IsNull())
            {
                services.AddScoped(typeof(TService), typeof(TAspNetCore));

                return;
            }

            if (current!.ImplementationType != typeof(TDefault))
                return;

            services.Replace(ServiceDescriptor.Scoped(typeof(TService), typeof(TAspNetCore)));
        }
    }
}