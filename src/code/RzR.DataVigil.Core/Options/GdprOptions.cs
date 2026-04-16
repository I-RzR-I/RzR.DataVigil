// ***********************************************************************
//  Assembly         : RzR.DataVigil.Core
//  Author           : RzR
//  Created On       : 2026-04-14 19:04
// 
//  Last Modified By : RzR
//  Last Modified On : 2026-04-14 19:41
// ***********************************************************************
//  <copyright file="GdprOptions.cs" company="RzR SOFT & TECH">
//   Copyright © RzR. All rights reserved.
//  </copyright>
// 
//  <summary>
//  </summary>
// ***********************************************************************

#region U S A G E S

using System;
using RzR.DataVigil.Abstractions.Contracts;
using RzR.DataVigil.Core.Gdpr;

#endregion

namespace RzR.DataVigil.Core.Options
{
    /// -------------------------------------------------------------------------------------------------
    /// <summary>
    ///     Configuration options for GDPR policies.
    /// </summary>
    /// =================================================================================================
    public sealed class GdprOptions
    {
        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Gets the registry.
        /// </summary>
        /// <value>
        ///     The registry.
        /// </value>
        /// =================================================================================================
        internal GdprPolicyRegistry Registry { get; } = new GdprPolicyRegistry();

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Configure GDPR policies for a specific entity type.
        /// </summary>
        /// <typeparam name="T">Generic type parameter.</typeparam>
        /// <param name="configure">The entity GDPR configure.</param>
        /// <returns>
        ///     The GdprOptions.
        /// </returns>
        /// =================================================================================================
        public GdprOptions ForEntity<T>(Action<EntityGdprPolicyBuilder<T>> configure)
            where T : class, IAuditable
        {
            var builder = new EntityGdprPolicyBuilder<T>();
            configure(builder);

            var policy = builder.Build();
            Registry.Register(typeof(T), policy);

            return this;
        }
    }
}