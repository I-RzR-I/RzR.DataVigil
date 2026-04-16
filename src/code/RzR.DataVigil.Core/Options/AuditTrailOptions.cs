// ***********************************************************************
//  Assembly         : RzR.DataVigil.Core
//  Author           : RzR
//  Created On       : 2026-04-14 19:04
// 
//  Last Modified By : RzR
//  Last Modified On : 2026-04-14 19:39
// ***********************************************************************
//  <copyright file="AuditTrailOptions.cs" company="RzR SOFT & TECH">
//   Copyright © RzR. All rights reserved.
//  </copyright>
// 
//  <summary>
//  </summary>
// ***********************************************************************

#region U S A G E S

using System;
using System.Collections.Generic;
using RzR.DataVigil.Abstractions.Services;

#endregion

namespace RzR.DataVigil.Core.Options
{
    /// -------------------------------------------------------------------------------------------------
    /// <summary>
    ///     Main options for configuring the audit trail system.
    /// </summary>
    /// =================================================================================================
    public sealed class AuditTrailOptions
    {
        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Gets the ef core.
        /// </summary>
        /// <value>
        ///     The ef core.
        /// </value>
        /// =================================================================================================
        public EfCoreAuditOptions EfCore { get; } = new EfCoreAuditOptions();

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Gets the storage.
        /// </summary>
        /// <value>
        ///     The storage.
        /// </value>
        /// =================================================================================================
        public StorageOptions Storage { get; } = new StorageOptions();

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Gets the gdpr.
        /// </summary>
        /// <value>
        ///     The gdpr.
        /// </value>
        /// =================================================================================================
        public GdprOptions Gdpr { get; } = new GdprOptions();

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Entity types excluded globally from audit — applies to all ORMs.
        /// </summary>
        /// <value>
        ///     The global exclusions.
        /// </value>
        /// =================================================================================================
        public ICollection<Type> GlobalExclusions { get; } = new HashSet<Type>();

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Factory for creating the user resolver.
        /// </summary>
        /// <value>
        ///     The type of the user resolver.
        /// </value>
        /// =================================================================================================
        internal Type UserResolverType { get; private set; }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Factory for creating the source resolver.
        /// </summary>
        /// <value>
        ///     The type of the source resolver.
        /// </value>
        /// =================================================================================================
        internal Type SourceResolverType { get; private set; }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Exclude an entity type globally from audit.
        /// </summary>
        /// <typeparam name="TEntity">Type of the entity.</typeparam>
        /// <returns>
        ///     The AuditTrailOptions.
        /// </returns>
        /// =================================================================================================
        public AuditTrailOptions Exclude<TEntity>() where TEntity : class
        {
            GlobalExclusions.Add(typeof(TEntity));

            return this;
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Register a custom user resolver.
        /// </summary>
        /// <typeparam name="TResolver">Type of the resolver.</typeparam>
        /// <returns>
        ///     The AuditTrailOptions.
        /// </returns>
        /// =================================================================================================
        public AuditTrailOptions UseUserResolver<TResolver>()
            where TResolver : class, IAuditUserResolver
        {
            UserResolverType = typeof(TResolver);

            return this;
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Register a custom source resolver.
        /// </summary>
        /// <typeparam name="TResolver">Type of the resolver.</typeparam>
        /// <returns>
        ///     The AuditTrailOptions.
        /// </returns>
        /// =================================================================================================
        public AuditTrailOptions UseSourceResolver<TResolver>()
            where TResolver : class, IAuditSourceResolver
        {
            SourceResolverType = typeof(TResolver);

            return this;
        }
    }
}