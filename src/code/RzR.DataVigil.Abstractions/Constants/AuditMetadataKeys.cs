// ***********************************************************************
//  Assembly         : RzR.DataVigil.Abstractions
//  Author           : RzR
//  Created On       : 2026-08-18 22:40
//
//  Last Modified By : RzR
//  Last Modified On : 2026-08-18 22:40
// ***********************************************************************
//  <copyright file="AuditMetadataKeys.cs" company="RzR SOFT & TECH">
//   Copyright © RzR. All rights reserved.
//  </copyright>
//
//  <summary>
//  </summary>
// ***********************************************************************

namespace RzR.DataVigil.Abstractions.Constants
{
    /// -------------------------------------------------------------------------------------------------
    /// <summary>
    ///     Reserved keys the library itself writes into
    ///     <see cref="T:RzR.DataVigil.Abstractions.Models.Entries.AuditTransaction"/>.Metadata. That
    ///     dictionary is public and consumer-writable, so every key the library owns is namespaced to
    ///     avoid colliding with keys a consumer might add.
    /// </summary>
    /// =================================================================================================
    public static class AuditMetadataKeys
    {
        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Metadata key under which the pipeline records the
        ///     <see cref="T:RzR.DataVigil.Abstractions.Enums.AuditUserSource"/> (as its
        ///     <c>ToString()</c>) describing how the transaction's actor was determined.
        /// </summary>
        /// =================================================================================================
        public const string UserSource = "__datavigil.user.source";
    }
}
