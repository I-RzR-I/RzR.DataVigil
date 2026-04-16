// ***********************************************************************
//  Assembly         : RzR.DataVigil.Core
//  Author           : RzR
//  Created On       : 2026-04-14 19:04
// 
//  Last Modified By : RzR
//  Last Modified On : 2026-04-14 19:14
// ***********************************************************************
//  <copyright file="EntityGdprPolicy.cs" company="RzR SOFT & TECH">
//   Copyright © RzR. All rights reserved.
//  </copyright>
// 
//  <summary>
//  </summary>
// ***********************************************************************

#region U S A G E S

using System;
using System.Collections.Generic;
using RzR.DataVigil.Abstractions.Models.Gdpr;

#endregion

namespace RzR.DataVigil.Core.Gdpr
{
    /// -------------------------------------------------------------------------------------------------
    /// <summary>
    ///     GDPR policy for a specific entity type, containing storage and retrieval rules.
    /// </summary>
    /// =================================================================================================
    public sealed class EntityGdprPolicy
    {
        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Gets or sets the type of the entity.
        /// </summary>
        /// <value>
        ///     The type of the entity.
        /// </value>
        /// =================================================================================================
        public Type EntityType { get; set; }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Gets or sets the storage rules.
        /// </summary>
        /// <value>
        ///     The storage rules.
        /// </value>
        /// =================================================================================================
        public IEnumerable<FieldGdprRule> StorageRules { get; set; }
            = Array.Empty<FieldGdprRule>();

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Gets or sets the retrieval rules.
        /// </summary>
        /// <value>
        ///     The retrieval rules.
        /// </value>
        /// =================================================================================================
        public IEnumerable<FieldGdprRule> RetrievalRules { get; set; }
            = Array.Empty<FieldGdprRule>();
    }
}