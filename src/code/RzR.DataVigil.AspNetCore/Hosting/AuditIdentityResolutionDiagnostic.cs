// ***********************************************************************
//  Assembly         : RzR.DataVigil.AspNetCore
//  Author           : RzR
//  Created On       : 2026-08-18 20:10
//
//  Last Modified By : RzR
//  Last Modified On : 2026-08-19 00:30
// ***********************************************************************
//  <copyright file="AuditIdentityResolutionDiagnostic.cs" company="RzR SOFT & TECH">
//   Copyright © RzR. All rights reserved.
//  </copyright>
//
//  <summary>
//  </summary>
// ***********************************************************************

#region U S A G E S

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RzR.DataVigil.Abstractions.Services;
using RzR.DataVigil.AspNetCore.Resolvers;
using RzR.DataVigil.Core.Resolvers;
using RzR.Extensions.Domain.Primitives;

#endregion

namespace RzR.DataVigil.AspNetCore.Hosting
{
    /// -------------------------------------------------------------------------------------------------
    /// <summary>
    ///     Startup diagnostic that warns, but never throws, when
    ///     <c>AddAuditTrailAspNetCore()</c> was called but <see cref="IAuditUserResolver"/> still resolves to the built-in
    ///     <see cref="DefaultUserResolver"/> — the symptom of a registration-order mistake that would
    ///     otherwise silently drop HTTP identity from every audit record.
    ///     <para>
    ///     A resolved type of <see cref="AspNetCoreUserResolver"/> or any other custom type is left
    ///     silent: a web host may legitimately run a custom non-HTTP resolver, and a library must not
    ///     fail a host's startup on a heuristic.
    ///     </para>
    /// </summary>
    /// <seealso cref="T:Microsoft.Extensions.Hosting.IHostedService"/>
    /// =================================================================================================
    internal sealed class AuditIdentityResolutionDiagnostic : IHostedService
    {
        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     (Immutable) the service provider.
        /// </summary>
        /// =================================================================================================
        private readonly IServiceProvider _serviceProvider;

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     (Immutable) the logger.
        /// </summary>
        /// =================================================================================================
        private readonly ILogger<AuditIdentityResolutionDiagnostic> _logger;

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Initializes a new instance of the <see cref="AuditIdentityResolutionDiagnostic"/> class.
        /// </summary>
        /// <param name="serviceProvider">The service provider.</param>
        /// <param name="logger">The logger.</param>
        /// =================================================================================================
        public AuditIdentityResolutionDiagnostic(IServiceProvider serviceProvider,
            ILogger<AuditIdentityResolutionDiagnostic> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        /// <inheritdoc/>
        public Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var resolver = scope.ServiceProvider.GetService<IAuditUserResolver>();

                if (resolver.IsNull() || resolver is AspNetCoreUserResolver)
                    return Task.CompletedTask;

                if ((resolver is DefaultUserResolver).IsFalse())
                    return Task.CompletedTask;

                _logger.LogWarning(
                    "AddAuditTrailAspNetCore() was called but IAuditUserResolver resolves to {Resolver}. "
                    + "Audit records will not carry HTTP identity. This usually means another "
                    + "registration claimed the contract after AddAuditTrailAspNetCore().",
                    resolver.GetType().Name);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not verify the configured IAuditUserResolver.");
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
