// ***********************************************************************
//  Assembly         : RzR.DataVigil.AspNetCore
//  Author           : RzR
//  Created On       : 2026-04-11 00:04
// 
//  Last Modified By : RzR
//  Last Modified On : 2026-08-28 21:10
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
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;
using RzR.DataVigil.Abstractions.Constants;
using RzR.DataVigil.Abstractions.Services;
using RzR.DataVigil.Core.Enums;
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

        /// <summary>
        ///     Never null; a no-op logger stands in when the host supplied none.
        /// </summary>
        private readonly ILogger<AspNetCoreCorrelationProvider> _logger;

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
            : this(httpContextAccessor, scopeContext, null)
        {
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Initializes a new instance of the <see cref="AspNetCoreCorrelationProvider"/> class.
        /// </summary>
        /// <param name="httpContextAccessor">The HTTP context accessor.</param>
        /// <param name="scopeContext">The scope context supplying a manual correlation id override.</param>
        /// <param name="logger">
        ///     The logger used to report a correlation id that was supplied and then refused. Optional; a
        ///     no-op logger stands in when it is null. A container picks this constructor only when
        ///     <see cref="IAuditScopeContext"/> is registered as well, so a host that registers logging
        ///     but no scope context reports nothing.
        /// </param>
        /// =================================================================================================
        public AspNetCoreCorrelationProvider(
            IHttpContextAccessor httpContextAccessor,
            IAuditScopeContext scopeContext,
            ILogger<AspNetCoreCorrelationProvider> logger)
        {
            _httpContextAccessor = httpContextAccessor;
            _scopeContext = scopeContext;
            _logger = logger ?? NullLogger<AspNetCoreCorrelationProvider>.Instance;
        }

        /// <inheritdoc/>
        public IResult<string> GetCorrelationId()
        {
            try
            {
                var scoped = _scopeContext?.GetCurrentCorrelationId();
                if (scoped.IsNotNull() && scoped!.IsSuccess)
                {
                    if (CorrelationIdValidator.TryAccept(scoped.Response, out var validScoped,
                            out var scopeReason, out var scopeInvalidIndex, out var scopeLength))
                        return Result<string>.Success(validScoped);

                    LogRejectedCorrelationId(
                        "AuditScope", LogLevel.Warning, scopeReason, scopeInvalidIndex, scopeLength);
                }

                var headers = _httpContextAccessor.HttpContext?.Request?.Headers;
                if (headers.IsNotNull())
                {
                    if (headers!.TryGetValue("X-Correlation-Id", out var correlationId)
                        && TryGetHeaderValue(correlationId, "X-Correlation-Id header", out var validCorrelationId))
                        return Result<string>.Success(validCorrelationId);

                    if (headers.TryGetValue("X-Request-Id", out var requestId)
                        && TryGetHeaderValue(requestId, "X-Request-Id header", out var validRequestId))
                        return Result<string>.Success(validRequestId);
                }

                var httpContext = _httpContextAccessor.HttpContext;
                if (httpContext.IsNotNull())
                {
                    if (CorrelationIdValidator.TryAccept(httpContext!.TraceIdentifier,
                            out var validTraceIdentifier,
                            out var traceReason, out var traceInvalidIndex, out var traceLength))
                        return Result<string>.Success(validTraceIdentifier);

                    LogRejectedCorrelationId(
                        "HttpContext.TraceIdentifier", LogLevel.Debug, traceReason, traceInvalidIndex, traceLength);
                }

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
        ///     Takes the first value of a client-supplied correlation header and accepts it only if
        ///     <see cref="CorrelationIdValidator"/> considers it safe to store. Rejected values fall
        ///     through to the next source rather than failing, so an audit record is always written.
        /// </summary>
        /// <param name="values">The raw header values.</param>
        /// <param name="source">A fixed literal naming the header, never read from the request itself.</param>
        /// <param name="value">[out] The accepted value, or null.</param>
        /// <returns>
        ///     True if the header carries an acceptable value.
        /// </returns>
        /// =================================================================================================
        private bool TryGetHeaderValue(StringValues values, string source, out string value)
        {
            var accepted = CorrelationIdValidator.TryAccept(values.Count > 0 ? values[0] : null, out value,
                out var reason, out var invalidIndex, out var candidateLength);

            if (accepted.IsFalse())
                LogRejectedCorrelationId(source, LogLevel.Debug, reason, invalidIndex, candidateLength);

            return accepted;
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Reports a correlation id that was supplied and then refused. A candidate that was never
        ///     supplied stays silent, so an ordinary request carrying no correlation header emits nothing.
        ///     Client-supplied sources report at Debug, which is off by default, because a caller can
        ///     drive them at will; only a developer-set scope value is a defect worth a Warning. The
        ///     rejected value itself is never logged because it is unvalidated input that may carry
        ///     control characters or a secret.
        /// </summary>
        /// <param name="source">A fixed literal naming the source that supplied the candidate.</param>
        /// <param name="level">The level to report at, decided by who supplied the candidate.</param>
        /// <param name="reason">Why the candidate was rejected.</param>
        /// <param name="invalidIndex">The index of the first disallowed character, or -1.</param>
        /// <param name="candidateLength">The trimmed length of the candidate.</param>
        /// =================================================================================================
        private void LogRejectedCorrelationId(
            string source, LogLevel level, CorrelationIdRejection reason, int invalidIndex, int candidateLength)
        {
            try
            {
                if (reason != CorrelationIdRejection.TooLong
                    && reason != CorrelationIdRejection.DisallowedCharacter)
                    return;

                if (level == LogLevel.Debug && _logger.IsEnabled(LogLevel.Debug).IsFalse())
                    return;

                switch (reason)
                {
                    case CorrelationIdRejection.TooLong:
                        _logger.Log(level,
                            "DataVigil correlation id from {CorrelationSource} was rejected: length "
                            + "{CandidateLength} exceeds the maximum of {MaxCorrelationIdLength}. Falling "
                            + "through to the next source. The rejected value is not logged because it is "
                            + "unvalidated input.",
                            source, candidateLength, AuditColumnLengths.CorrelationId);
                        break;

                    case CorrelationIdRejection.DisallowedCharacter:
                        _logger.Log(level,
                            "DataVigil correlation id from {CorrelationSource} was rejected: disallowed "
                            + "character at index {InvalidCharacterIndex} of {CandidateLength}. Allowed "
                            + "characters are A-Z a-z 0-9 and . _ : - only. Falling through to the next "
                            + "source. The rejected value is not logged because it is unvalidated input.",
                            source, invalidIndex, candidateLength);
                        break;
                }
            }
            catch
            {
                /* ignored */
            }
        }
    }
}
