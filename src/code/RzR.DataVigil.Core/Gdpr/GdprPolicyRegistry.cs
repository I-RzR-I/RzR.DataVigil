// ***********************************************************************
//  Assembly         : RzR.DataVigil.Core
//  Author           : RzR
//  Created On       : 2026-04-10 22:04
// 
//  Last Modified By : RzR
//  Last Modified On : 2026-04-14 19:14
// ***********************************************************************
//  <copyright file="GdprPolicyRegistry.cs" company="RzR SOFT & TECH">
//   Copyright © RzR. All rights reserved.
//  </copyright>
// 
//  <summary>
//  </summary>
// ***********************************************************************

#region U S A G E S

using System;
using System.Collections.Generic;

#endregion

namespace RzR.DataVigil.Core.Gdpr
{
    /// -------------------------------------------------------------------------------------------------
    /// <summary>
    ///     Registry that holds all configured GDPR policies per entity type.
    /// </summary>
    /// =================================================================================================
    public sealed class GdprPolicyRegistry
    {
        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     (Immutable) name of the policies by.
        /// </summary>
        /// =================================================================================================
        private readonly IDictionary<string, EntityGdprPolicy> _policiesByName
            = new Dictionary<string, EntityGdprPolicy>(StringComparer.Ordinal);

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     (Immutable) type of the policies by.
        /// </summary>
        /// =================================================================================================
        private readonly IDictionary<Type, EntityGdprPolicy> _policiesByType
            = new Dictionary<Type, EntityGdprPolicy>();

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Registers this object.
        /// </summary>
        /// <param name="entityType">Type of the entity.</param>
        /// <param name="policy">[out] The policy.</param>
        /// =================================================================================================
        internal void Register(Type entityType, EntityGdprPolicy policy)
        {
            _policiesByType[entityType] = policy;
            _policiesByName[entityType.Name] = policy;
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Attempts to get policy an EntityGdprPolicy from the given Type.
        /// </summary>
        /// <param name="entityType">Type of the entity.</param>
        /// <param name="policy">[out] The policy.</param>
        /// <returns>
        ///     True if it succeeds, false if it fails.
        /// </returns>
        /// =================================================================================================
        public bool TryGetPolicy(Type entityType, out EntityGdprPolicy policy)
        {
            return _policiesByType.TryGetValue(entityType, out policy);
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Attempts to get policy by name an EntityGdprPolicy from the given string.
        /// </summary>
        /// <param name="entityName">Name of the entity.</param>
        /// <param name="policy">[out] The policy.</param>
        /// <returns>
        ///     True if it succeeds, false if it fails.
        /// </returns>
        /// =================================================================================================
        public bool TryGetPolicyByName(string entityName, out EntityGdprPolicy policy)
        {
            return _policiesByName.TryGetValue(entityName, out policy);
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Query if 'entityType' has policy.
        /// </summary>
        /// <param name="entityType">Type of the entity.</param>
        /// <returns>
        ///     True if policy, false if not.
        /// </returns>
        /// =================================================================================================
        public bool HasPolicy(Type entityType)
        {
            return _policiesByType.ContainsKey(entityType);
        }
    }
}