// ***********************************************************************
//  Assembly         : RzR.DataVigil.Core
//  Author           : RzR
//  Created On       : 2026-04-10 23:04
// 
//  Last Modified By : RzR
//  Last Modified On : 2026-04-14 19:08
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
using Microsoft.Extensions.DependencyInjection;
using RzR.DataVigil.Core.Builder;
using RzR.DataVigil.Core.Hosting;
using RzR.DataVigil.Core.Options;

#endregion

namespace RzR.DataVigil.Core.Extensions
{
    /// -------------------------------------------------------------------------------------------------
    /// <summary>
    ///     Extension methods for registering the audit trail in DI.
    /// </summary>
    /// =================================================================================================
    public static class ServiceCollectionExtensions
    {
        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Adds the audit trail system to the service collection.
        /// </summary>
        /// <param name="services">The services to act on.</param>
        /// <param name="configure">The audit configure option.</param>
        /// <returns>
        ///     An AuditTrailBuilder.
        /// </returns>
        /// =================================================================================================
        public static AuditTrailBuilder AddAuditTrail(
            this IServiceCollection services,
            Action<AuditTrailOptions> configure)
        {
            var builder = new AuditTrailBuilder(services);
            configure(builder.Options);
            builder.Build();

            return builder;
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Registers the audit retention background service. Purges entries older than
        ///     StorageOptions.RetentionDays every 24 hours. No-op at runtime if RetentionDays is not
        ///     configured.
        /// </summary>
        /// <param name="services">The services to act on.</param>
        /// <returns>
        ///     An IServiceCollection.
        /// </returns>
        /// =================================================================================================
        public static IServiceCollection AddAuditRetentionService(this IServiceCollection services)
        {
            services.AddHostedService<AuditRetentionService>();

            return services;
        }
    }
}