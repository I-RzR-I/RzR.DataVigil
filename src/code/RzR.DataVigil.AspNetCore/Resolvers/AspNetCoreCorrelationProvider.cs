// ***********************************************************************
//  Assembly         : RzR.DataVigil.AspNetCore
//  Author           : RzR
//  Created On       : 2026-04-11 00:04
// 
//  Last Modified By : RzR
//  Last Modified On : 2026-04-14 20:14
// ***********************************************************************
//  <copyright file="AspNetCoreCorrelationProvider.cs" company="RzR SOFT & TECH">
//   Copyright © RzR. All rights reserved.
//  </copyright>
// 
//  <summary>
//  </summary>
// ***********************************************************************

#region U S A G E S

using System;
using System.Diagnostics;
using AggregatedGenericResultMessage;
using AggregatedGenericResultMessage.Abstractions;
using AggregatedGenericResultMessage.Extensions.Result;
using DomainCommonExtensions.CommonExtensions;
using Microsoft.AspNetCore.Http;
using RzR.DataVigil.Abstractions.Services;

#endregion

namespace RzR.DataVigil.AspNetCore.Resolvers
{
    /// -------------------------------------------------------------------------------------------------
    /// <summary>
    ///     Provides correlation and trace IDs from HTTP request headers, falling back to
    ///     Activity.Current (OpenTelemetry).
    /// </summary>
    /// <seealso cref="T:RzR.DataVigil.Abstractions.Services.IAuditCorrelationProvider"/>
    /// =================================================================================================
    public class AspNetCoreCorrelationProvider : IAuditCorrelationProvider
    {
        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     (Immutable) the HTTP context accessor.
        /// </summary>
        /// =================================================================================================
        private readonly IHttpContextAccessor _httpContextAccessor;

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Initializes a new instance of the <see cref="AspNetCoreCorrelationProvider"/> class.
        /// </summary>
        /// <param name="httpContextAccessor">The HTTP context accessor.</param>
        /// =================================================================================================
        public AspNetCoreCorrelationProvider(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        /// <inheritdoc/>
        public IResult<string> GetCorrelationId()
        {
            try
            {
                var headers = _httpContextAccessor.HttpContext?.Request?.Headers;
                if (headers.IsNotNull())
                {
                    if (headers!.TryGetValue("X-Correlation-Id", out var correlationId)
                        && !string.IsNullOrWhiteSpace(correlationId))
                        return Result<string>.Success(correlationId);

                    if (headers.TryGetValue("X-Request-Id", out var requestId)
                        && !string.IsNullOrWhiteSpace(requestId))
                        return Result<string>.Success(requestId);
                }

                return Result<string>.Success(Activity.Current?.Id);
            }
            catch (Exception e)
            {
                return Result<string>
                    .Failure(e.Message)
                    .WithError(e);
            }
        }

        /// <inheritdoc/>
        public IResult<string> GetTraceId()
        {
            try
            {
                var httpContext = _httpContextAccessor.HttpContext;
                if (httpContext.IsNotNull())
                    return Result<string>.Success(httpContext.TraceIdentifier);

                return Result<string>.Success(Activity.Current?.TraceId.ToString());
            }
            catch (Exception e)
            {
                return Result<string>
                    .Failure(e.Message)
                    .WithError(e);
            }
        }
    }
}