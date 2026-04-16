// ***********************************************************************
//  Assembly         : RzR.DataVigil.Abstractions
//  Author           : RzR
//  Created On       : 2026-04-10 22:04
// 
//  Last Modified By : RzR
//  Last Modified On : 2026-04-11 03:22
// ***********************************************************************
//  <copyright file="GdprRetrievalContext.cs" company="RzR SOFT & TECH">
//   Copyright © RzR. All rights reserved.
//  </copyright>
// 
//  <summary>
//  </summary>
// ***********************************************************************

#region U S A G E S

using System.Collections.Generic;
using System.Linq;
using RzR.DataVigil.Abstractions.Extensions;

#endregion

namespace RzR.DataVigil.Abstractions.Models.Gdpr
{
    /// -------------------------------------------------------------------------------------------------
    /// <summary>
    ///     Context of the user requesting audit data retrieval. Used by the GDPR processor to
    ///     evaluate AllowedRoles/AllowedClaims.
    /// </summary>
    /// =================================================================================================
    public class GdprRetrievalContext
    {
        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Roles assigned to the requesting user.
        /// </summary>
        /// <value>
        ///     The user roles.
        /// </value>
        /// =================================================================================================
        public IEnumerable<string> UserRoles { get; set; } = new List<string>();

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Claims of the requesting user. Key = claim type, Value = claim value.
        /// </summary>
        /// <value>
        ///     The user claims.
        /// </value>
        /// =================================================================================================
        public IDictionary<string, string> UserClaims { get; set; } = new Dictionary<string, string>();

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Evaluates whether the user has the right to view unmasked data for a field protected by
        ///     the given rule.
        /// </summary>
        /// <param name="rule">The rule.</param>
        /// <returns>
        ///     True if we can access, false if not.
        /// </returns>
        /// =================================================================================================
        public bool CanAccess(FieldGdprRule rule)
        {
            if (rule.AllowedRoles.IsNotNullOrEmptyEnumerable())
            {
                foreach (var role in rule.AllowedRoles)
                {
                    if (UserRoles.Contains(role))
                        return true;
                }
            }

            if (rule.AllowedClaims.IsNotNullOrEmptyEnumerable())
            {
                foreach (var kvp in rule.AllowedClaims)
                {
                    if (UserClaims.TryGetValue(kvp.Key, out var val) && val == kvp.Value)
                        return true;
                }
            }

            return false;
        }
    }
}