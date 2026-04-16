// ***********************************************************************
//  Assembly         : RzR.DataVigil.Core
//  Author           : RzR
//  Created On       : 2026-04-10 23:04
// 
//  Last Modified By : RzR
//  Last Modified On : 2026-04-14 20:11
// ***********************************************************************
//  <copyright file="DefaultCorrelationProvider.cs" company="RzR SOFT & TECH">
//   Copyright © RzR. All rights reserved.
//  </copyright>
// 
//  <summary>
//  </summary>
// ***********************************************************************

#region U S A G E S

using System.Diagnostics;
using AggregatedGenericResultMessage;
using AggregatedGenericResultMessage.Abstractions;
using RzR.DataVigil.Abstractions.Services;

#endregion

namespace RzR.DataVigil.Core.Resolvers
{
    /// -------------------------------------------------------------------------------------------------
    /// <summary>
    ///     Default correlation provider using System.Diagnostics.Activity (OpenTelemetry-compatible).
    /// </summary>
    /// <seealso cref="T:RzR.DataVigil.Abstractions.Services.IAuditCorrelationProvider"/>
    /// =================================================================================================
    public class DefaultCorrelationProvider : IAuditCorrelationProvider
    {
        /// <inheritdoc/>
        public IResult<string> GetCorrelationId()
        {
            return Result<string>.Success(Activity.Current?.Id);
        }

        /// <inheritdoc/>
        public IResult<string> GetTraceId()
        {
            return Result<string>.Success(Activity.Current?.TraceId.ToString());
        }
    }
}