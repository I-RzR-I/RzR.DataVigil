// ***********************************************************************
//  Assembly         : RzR.DataVigil.Core
//  Author           : RzR
//  Created On       : 2026-04-10 22:04
// 
//  Last Modified By : RzR
//  Last Modified On : 2026-04-14 19:13
// ***********************************************************************
//  <copyright file="GdprAccessBuilder.cs" company="RzR SOFT & TECH">
//   Copyright © RzR. All rights reserved.
//  </copyright>
// 
//  <summary>
//  </summary>
// ***********************************************************************

#region U S A G E S

using System.Collections.Generic;

#endregion

namespace RzR.DataVigil.Core.Gdpr
{
    /// -------------------------------------------------------------------------------------------------
    /// <summary>
    ///     Builder for configuring retrieval access conditions (roles + claims).
    /// </summary>
    /// =================================================================================================
    public sealed class GdprAccessBuilder
    {
        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Gets the roles.
        /// </summary>
        /// <value>
        ///     The roles.
        /// </value>
        /// =================================================================================================
        internal IList<string> Roles { get; } = new List<string>();

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Gets the claims.
        /// </summary>
        /// <value>
        ///     The claims.
        /// </value>
        /// =================================================================================================
        internal IDictionary<string, string> Claims { get; }
            = new Dictionary<string, string>();

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Allow roles.
        /// </summary>
        /// <param name="roles">A variable-length parameters list containing roles.</param>
        /// <returns>
        ///     A GdprAccessBuilder.
        /// </returns>
        /// =================================================================================================
        public GdprAccessBuilder AllowRoles(params string[] roles)
        {
            foreach (var r in roles) Roles.Add(r);

            return this;
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Allow claim.
        /// </summary>
        /// <param name="claimType">Type of the claim.</param>
        /// <param name="claimValue">The claim value.</param>
        /// <returns>
        ///     A GdprAccessBuilder.
        /// </returns>
        /// =================================================================================================
        public GdprAccessBuilder AllowClaim(string claimType, string claimValue)
        {
            Claims[claimType] = claimValue;

            return this;
        }
    }
}