// ***********************************************************************
//  Assembly         : RzR.DataVigil.Abstractions
//  Author           : RzR
//  Created On       : 2026-04-15 01:04
// 
//  Last Modified By : RzR
//  Last Modified On : 2026-04-15 01:14
// ***********************************************************************
//  <copyright file="AuditTransactionQuery.cs" company="RzR SOFT & TECH">
//   Copyright © RzR. All rights reserved.
//  </copyright>
// 
//  <summary>
//  </summary>
// ***********************************************************************

namespace RzR.DataVigil.Abstractions.Models.Query
{
    /// -------------------------------------------------------------------------------------------------
    /// <summary>
    ///     Query parameters for paginating audit transaction results.
    /// </summary>
    /// =================================================================================================
    public class AuditTransactionQuery
    {
        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Gets or sets the number of records to skip (offset). Default is 0.
        /// </summary>
        /// =================================================================================================
        public int Skip { get; set; } = 0;

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Gets or sets the maximum number of records to return. Default is 10.
        /// </summary>
        /// =================================================================================================
        public int Take { get; set; } = 10;
    }
}

