// ***********************************************************************
//  Assembly         : RzR.DataVigil.Abstractions
//  Author           : RzR
//  Created On       : 2026-08-27 14:20
// 
//  Last Modified By : RzR
//  Last Modified On : 2026-08-28 20:40
// ***********************************************************************
//  <copyright file="IAuditMetadataEnricher.cs" company="RzR SOFT & TECH">
//   Copyright © RzR. All rights reserved.
//  </copyright>
// 
//  <summary>
//  </summary>
// ***********************************************************************

#region U S A G E S

using System.Collections.Generic;
using RzR.ResultMessage.Abstractions;

#endregion

namespace RzR.DataVigil.Abstractions.Services
{
    /// -------------------------------------------------------------------------------------------------
    /// <summary>
    ///     Contributes ambient key/value pairs to the Metadata dictionary of the
    ///     <see cref="T:RzR.DataVigil.Abstractions.Models.Entries.AuditTransaction"/> currently being
    ///     processed. Several enrichers may be registered; the pipeline runs all of them.
    /// </summary>
    /// =================================================================================================
    public interface IAuditMetadataEnricher
    {
        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Returns the metadata pairs describing the current ambient operation.
        /// </summary>
        /// <returns>
        ///     An IResult whose response carries the pairs to write, possibly empty, never expected to
        ///     be null on success.
        /// </returns>
        /// =================================================================================================
        IResult<IEnumerable<KeyValuePair<string, string>>> Enrich();
    }
}
