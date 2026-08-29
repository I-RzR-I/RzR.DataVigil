// ***********************************************************************
//  Assembly         : RzR.DataVigil.Abstractions
//  Author           : RzR
//  Created On       : 2026-08-18 22:40
//
//  Last Modified By : RzR
//  Last Modified On : 2026-08-28 20:40
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

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Metadata key under which the HTTP method of the request that produced the transaction is
        ///     recorded, for example <c>GET</c> or <c>POST</c>. Absent when the change did not arrive
        ///     over HTTP, such as in a worker or a background task.
        /// </summary>
        /// =================================================================================================
        public const string HttpMethod = "__datavigil.http.method";

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Metadata key under which the matched route <b>template</b> is recorded, for example
        ///     <c>/orders/{id}</c>.
        /// </summary>
        /// =================================================================================================
        public const string HttpRoute = "__datavigil.http.route";

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Metadata key under which the pipeline records the names of the transaction fields that
        ///     exceeded their storage column length, comma separated with no spaces, in the fixed order
        ///     <c>UserId,UserName,IpAddress,Source,CorrelationId,TraceId</c>.
        /// </summary>
        /// =================================================================================================
        public const string Oversize = "__datavigil.oversize";
    }
}
