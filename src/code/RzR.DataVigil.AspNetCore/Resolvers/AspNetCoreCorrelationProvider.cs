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
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using RzR.DataVigil.Abstractions.Services;
using RzR.DataVigil.Core.Helpers;
using RzR.Extensions.Domain.Primitives;
using RzR.ResultMessage;
using RzR.ResultMessage.Abstractions;
using RzR.ResultMessage.Extensions.Result;

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
        private const int MaxCorrelationIdLength = 128;

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     (Immutable) the HTTP context accessor.
        /// </summary>
        /// =================================================================================================
        private readonly IHttpContextAccessor _httpContextAccessor;

        /// <summary>
        ///     Optional scope override. Null when the provider is constructed directly.
        /// </summary>
        private readonly IAuditScopeContext _scopeContext;

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Initializes a new instance of the <see cref="AspNetCoreCorrelationProvider"/> class.
        /// </summary>
        /// <param name="httpContextAccessor">The HTTP context accessor.</param>
        /// =================================================================================================
        public AspNetCoreCorrelationProvider(IHttpContextAccessor httpContextAccessor)
            : this(httpContextAccessor, null)
        {
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Initializes a new instance of the <see cref="AspNetCoreCorrelationProvider"/> class.
        /// </summary>
        /// <param name="httpContextAccessor">The HTTP context accessor.</param>
        /// <param name="scopeContext">The scope context supplying a manual correlation id override.</param>
        /// =================================================================================================
        public AspNetCoreCorrelationProvider(
            IHttpContextAccessor httpContextAccessor,
            IAuditScopeContext scopeContext)
        {
            _httpContextAccessor = httpContextAccessor;
            _scopeContext = scopeContext;
        }

        /// <inheritdoc/>
        public IResult<string> GetCorrelationId()
        {
            try
            {
                var scoped = _scopeContext?.GetCurrentCorrelationId();
                if (scoped.IsNotNull() && scoped!.IsSuccess && string.IsNullOrEmpty(scoped.Response) == false)
                    return scoped;

                var headers = _httpContextAccessor.HttpContext?.Request?.Headers;
                if (headers.IsNotNull())
                {
                    if (headers!.TryGetValue("X-Correlation-Id", out var correlationId)
                        && TryGetHeaderValue(correlationId, out var validCorrelationId))
                        return Result<string>.Success(validCorrelationId);

                    if (headers.TryGetValue("X-Request-Id", out var requestId)
                        && TryGetHeaderValue(requestId, out var validRequestId))
                        return Result<string>.Success(validRequestId);
                }

                var httpContext = _httpContextAccessor.HttpContext;
                if (httpContext.IsNotNull())
                    return Result<string>.Success(httpContext!.TraceIdentifier);

                return Result<string>.Success(ActivityTraceHelper.GetW3CTraceId());
            }
            catch (Exception e)
            {
                return Result<string>
                    .Failure(e.Message)
                    .WithError(e);
            }
        }

        /// <inheritdoc />
        public IResult<string> GetTraceId()
        {
            try
            {
                return Result<string>.Success(ActivityTraceHelper.GetW3CTraceId());
            }
            catch (Exception e)
            {
                return Result<string>
                    .Failure(e.Message)
                    .WithError(e);
            }
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Takes the first value of a client-supplied correlation header and accepts it only if it is
        ///     short and simple enough to be safe to store. Rejected values fall through to the next
        ///     source rather than failing, so an audit record is always written.
        /// </summary>
        /// <param name="values">The raw header values.</param>
        /// <param name="value">[out] The accepted value, or null.</param>
        /// <returns>
        ///     True if the header carries an acceptable value.
        /// </returns>
        /// =================================================================================================
        private static bool TryGetHeaderValue(StringValues values, out string value)
        {
            var candidate = values.Count > 0 ? values[0]?.Trim() : null;

            value = string.IsNullOrEmpty(candidate) == false
                    && candidate.Length <= MaxCorrelationIdLength
                    && candidate.All(IsAllowedCorrelationChar)
                ? candidate
                : null;

            return value.IsNotNull();
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Allowed correlation-id characters: <c>[A-Za-z0-9._:-]</c>.
        /// </summary>
        /// <param name="c">The character to test.</param>
        /// <returns>
        ///     True if the character is allowed.
        /// </returns>
        /// =================================================================================================
        private static bool IsAllowedCorrelationChar(char c)
            => (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9')
               || c == '.' || c == '_' || c == ':' || c == '-';
    }
}