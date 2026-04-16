// ***********************************************************************
//  Assembly         : RzR.DataVigil.Abstractions
//  Author           : RzR
//  Created On       : 2026-04-10 22:04
// 
//  Last Modified By : RzR
//  Last Modified On : 2026-04-11 03:22
// ***********************************************************************
//  <copyright file="IAuditableEntity.cs" company="RzR SOFT & TECH">
//   Copyright © RzR. All rights reserved.
//  </copyright>
// 
//  <summary>
//  </summary>
// ***********************************************************************

#region U S A G E S

using System.Collections.Generic;
using RzR.DataVigil.Abstractions.Enums;

#endregion

namespace RzR.DataVigil.Abstractions.Contracts
{
    /// -------------------------------------------------------------------------------------------------
    /// <summary>
    ///     Granular audit control per entity. Optional — <see cref="IAuditable" /> is sufficient for
    ///     the simple case.
    /// </summary>
    /// =================================================================================================
    public interface IAuditableEntity : IAuditable
    {
        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Determines whether this instance should be audited for the given action.
        /// </summary>
        /// <param name="action">The action.</param>
        /// <returns>
        ///     True if it succeeds, false if it fails.
        /// </returns>
        /// =================================================================================================
        bool ShouldAudit(AuditAction action);

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Fields excluded from audit, supplementary to GDPR policies.
        /// </summary>
        /// <returns>
        ///     An enumerator that allows foreach to be used to process the excluded fields in this
        ///     collection.
        /// </returns>
        /// =================================================================================================
        IEnumerable<string> GetExcludedFields();
    }
}