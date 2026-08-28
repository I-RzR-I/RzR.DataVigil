// ***********************************************************************
//  Assembly         : RzR.DataVigil.AspNetCore
//  Author           : RzR
//  Created On       : 2026-08-28 21:10
//
//  Last Modified By : RzR
//  Last Modified On : 2026-08-28 21:10
// ***********************************************************************
//  <copyright file="HttpRouteTemplateAccessor.cs" company="RzR SOFT & TECH">
//   Copyright © RzR. All rights reserved.
//  </copyright>
//
//  <summary>
//  </summary>
// ***********************************************************************

#region U S A G E S

using System;
using Microsoft.AspNetCore.Http;

#endregion

namespace RzR.DataVigil.AspNetCore.Enrichers
{
    /// -------------------------------------------------------------------------------------------------
    /// <summary>
    ///     Carries the consumer-supplied route template accessor through the container so that
    ///     <see cref="T:RzR.DataVigil.AspNetCore.Enrichers.HttpOperationMetadataEnricher"/> can read it at
    ///     resolution time rather than at registration time. 
    /// </summary>
    /// =================================================================================================
    internal sealed class HttpRouteTemplateAccessor
    {
        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Initializes a new instance of the <see cref="HttpRouteTemplateAccessor"/> class.
        /// </summary>
        /// <param name="accessor">
        ///     Returns the matched route template for a context, or null when none matched.
        /// </param>
        /// =================================================================================================
        public HttpRouteTemplateAccessor(Func<HttpContext, string> accessor) => Accessor = accessor;

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     (Immutable) supplies the matched route template for a context.
        /// </summary>
        /// <value>
        ///     The accessor supplied by the consumer.
        /// </value>
        /// =================================================================================================
        public Func<HttpContext, string> Accessor { get; }
    }
}
