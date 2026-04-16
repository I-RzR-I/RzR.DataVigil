// ***********************************************************************
//  Assembly         : RzR.DataVigil.Core
//  Author           : RzR
//  Created On       : 2026-04-10 23:04
// 
//  Last Modified By : RzR
//  Last Modified On : 2026-04-14 19:34
// ***********************************************************************
//  <copyright file="AuditTrailBuilder.cs" company="RzR SOFT & TECH">
//   Copyright © RzR. All rights reserved.
//  </copyright>
// 
//  <summary>
//  </summary>
// ***********************************************************************

#region U S A G E S

using DomainCommonExtensions.CommonExtensions;
using Microsoft.Extensions.DependencyInjection;
using RzR.DataVigil.Abstractions.Services;
using RzR.DataVigil.Core.Gdpr;
using RzR.DataVigil.Core.Options;
using RzR.DataVigil.Core.Pipeline;
using RzR.DataVigil.Core.Resolvers;

#endregion

namespace RzR.DataVigil.Core.Builder
{
    /// -------------------------------------------------------------------------------------------------
    /// <summary>
    ///     Builder that registers all audit trail services into DI.
    /// </summary>
    /// =================================================================================================
    public sealed class AuditTrailBuilder
    {
        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Initializes a new instance of the <see cref="AuditTrailBuilder"/> class.
        /// </summary>
        /// <param name="services">The services.</param>
        /// =================================================================================================
        public AuditTrailBuilder(IServiceCollection services)
        {
            Services = services;
            Options = new AuditTrailOptions();
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Gets the services.
        /// </summary>
        /// <value>
        ///     The services.
        /// </value>
        /// =================================================================================================
        public IServiceCollection Services { get; }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Gets options for controlling the operation.
        /// </summary>
        /// <value>
        ///     The options.
        /// </value>
        /// =================================================================================================
        public AuditTrailOptions Options { get; }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Builds this object.
        /// </summary>
        /// =================================================================================================
        internal void Build()
        {
            // Register options as singleton
            Services.AddSingleton(Options);
            Services.AddSingleton(Options.EfCore);
            Services.AddSingleton(Options.Storage);

            // Register GDPR
            Services.AddSingleton(Options.Gdpr.Registry);
            Services.AddSingleton<GdprProcessor>();

            // Register pipeline
            Services.AddScoped<AuditPipeline>();

            // Register scope context
            Services.AddScoped<IAuditScopeContext, AuditScopeContext>();

            // Register user resolver
            if (Options.UserResolverType.IsNotNull())
                Services.AddScoped(typeof(IAuditUserResolver), Options.UserResolverType);
            else
                Services.AddScoped<IAuditUserResolver, DefaultUserResolver>();

            // Register source resolver
            if (Options.SourceResolverType.IsNotNull())
                Services.AddScoped(typeof(IAuditSourceResolver), Options.SourceResolverType);
            else
                Services.AddSingleton<IAuditSourceResolver, DefaultSourceResolver>();

            // Register correlation provider
            Services.AddScoped<IAuditCorrelationProvider, DefaultCorrelationProvider>();
        }
    }
}