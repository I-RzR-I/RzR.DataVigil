// ***********************************************************************
//  Assembly         : RzR.DataVigil.Abstractions
//  Author           : RzR
//  Created On       : 2026-04-10 22:04
// 
//  Last Modified By : RzR
//  Last Modified On : 2026-04-11 03:22
// ***********************************************************************
//  <copyright file="FieldGdprRule.cs" company="RzR SOFT & TECH">
//   Copyright © RzR. All rights reserved.
//  </copyright>
// 
//  <summary>
//  </summary>
// ***********************************************************************

#region U S A G E S

using System;
using System.Collections.Generic;
using RzR.DataVigil.Abstractions.Enums;

#endregion

namespace RzR.DataVigil.Abstractions.Models.Gdpr
{
    /// -------------------------------------------------------------------------------------------------
    /// <summary>
    ///     GDPR rule applied to a specific field of an audited entity.
    /// </summary>
    /// =================================================================================================
    public class FieldGdprRule
    {
        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Property name on the entity.
        /// </summary>
        /// <value>
        ///     The name of the field.
        /// </value>
        /// =================================================================================================
        public string FieldName { get; set; }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     GDPR action to apply.
        /// </summary>
        /// <value>
        ///     The action.
        /// </value>
        /// =================================================================================================
        public GdprFieldAction Action { get; set; }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Custom transformer — used only when Action = Custom.
        /// </summary>
        /// <value>
        ///     A function delegate that yields a string.
        /// </value>
        /// =================================================================================================
        public Func<string, string> CustomTransformer { get; set; }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Roles that allow viewing unmasked data. Null or empty = nobody sees unmasked via role.
        /// </summary>
        /// <value>
        ///     The allowed roles.
        /// </value>
        /// =================================================================================================
        public IEnumerable<string> AllowedRoles { get; set; }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Claims that allow viewing unmasked data. Key = claim type, Value = claim value. Null or
        ///     empty = nobody sees unmasked via claim.
        /// </summary>
        /// <value>
        ///     The allowed claims.
        /// </value>
        /// =================================================================================================
        public IDictionary<string, string> AllowedClaims { get; set; }
    }
}