// ***********************************************************************
//  Assembly         : RzR.DataVigil.Abstractions
//  Author           : RzR
//  Created On       : 2026-04-10 22:04
// 
//  Last Modified By : RzR
//  Last Modified On : 2026-04-11 03:22
// ***********************************************************************
//  <copyright file="AuditUserInfo.cs" company="RzR SOFT & TECH">
//   Copyright © RzR. All rights reserved.
//  </copyright>
// 
//  <summary>
//  </summary>
// ***********************************************************************

#region U S A G E S

using System.Collections.Generic;

#endregion

namespace RzR.DataVigil.Abstractions.Models.Identity
{
    /// -------------------------------------------------------------------------------------------------
    /// <summary>
    ///     Information about the user performing the audited action.
    /// </summary>
    /// =================================================================================================
    public class AuditUserInfo
    {
        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Unique identifier of the user (e.g. database PK, sub claim).
        /// </summary>
        /// <value>
        ///     The identifier of the user.
        /// </value>
        /// =================================================================================================
        public string UserId { get; set; }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Display name of the user.
        /// </summary>
        /// <value>
        ///     The name of the user.
        /// </value>
        /// =================================================================================================
        public string UserName { get; set; }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     IP address of the client, when available.
        /// </summary>
        /// <value>
        ///     The IP address.
        /// </value>
        /// =================================================================================================
        public string IpAddress { get; set; }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     User roles — used for GDPR retrieval policies.
        /// </summary>
        /// <value>
        ///     The roles.
        /// </value>
        /// =================================================================================================
        public IEnumerable<string> Roles { get; set; } = new List<string>();

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     User claims — used for GDPR retrieval policies. Key = claim type, Value = claim value.
        /// </summary>
        /// <value>
        ///     The claims.
        /// </value>
        /// =================================================================================================
        public IDictionary<string, string> Claims { get; set; }
            = new Dictionary<string, string>();
    }
}