// ***********************************************************************
//  Assembly         : RzR.DataVigil.Core
//  Author           : RzR
//  Created On       : 2026-04-10 23:04
// 
//  Last Modified By : RzR
//  Last Modified On : 2026-08-28 21:10
// ***********************************************************************
//  <copyright file="DefaultCorrelationProvider.cs" company="RzR SOFT & TECH">
//   Copyright © RzR. All rights reserved.
//  </copyright>
// 
//  <summary>
//  </summary>
// ***********************************************************************

#region U S A G E S

using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RzR.DataVigil.Abstractions.Constants;
using RzR.DataVigil.Abstractions.Services;
using RzR.DataVigil.Core.Enums;
using RzR.DataVigil.Core.Helpers;
using RzR.Extensions.Domain.Primitives;
using RzR.ResultMessage;
using RzR.ResultMessage.Abstractions;
using RzR.ResultMessage.Extensions.Result;

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
        /// <summary>
        ///     Optional scope override. Null when the provider is constructed directly.
        /// </summary>
        private readonly IAuditScopeContext _scopeContext;

        /// <summary>
        ///     Never null; a no-op logger stands in when the host supplied none.
        /// </summary>
        private readonly ILogger<DefaultCorrelationProvider> _logger;

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Initializes a new instance with no scope override.
        /// </summary>
        /// =================================================================================================
        public DefaultCorrelationProvider() : this(null)
        {
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Initializes a new instance of the <see cref="DefaultCorrelationProvider"/> class.
        /// </summary>
        /// <param name="scopeContext">The scope context supplying a manual correlation id override.</param>
        /// =================================================================================================
        public DefaultCorrelationProvider(IAuditScopeContext scopeContext)
            : this(scopeContext, null)
        {
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Initializes a new instance of the <see cref="DefaultCorrelationProvider"/> class.
        /// </summary>
        /// <param name="scopeContext">The scope context supplying a manual correlation id override.</param>
        /// <param name="logger">
        ///     The logger used to report a correlation id that was supplied and then refused. Optional; a
        ///     no-op logger stands in when it is null. A container picks this constructor only when
        ///     <see cref="IAuditScopeContext"/> is registered as well, so a host that registers logging
        ///     but no scope context reports nothing.
        /// </param>
        /// =================================================================================================
        public DefaultCorrelationProvider(IAuditScopeContext scopeContext,
            ILogger<DefaultCorrelationProvider> logger)
        {
            _scopeContext = scopeContext;
            _logger = logger ?? NullLogger<DefaultCorrelationProvider>.Instance;
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
                            out var reason, out var invalidIndex, out var candidateLength))
                        return Result<string>.Success(validScoped);

                    LogRejectedCorrelationId("AuditScope", reason, invalidIndex, candidateLength);
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

        /// <inheritdoc/>
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
        ///     Reports a correlation id that was supplied and then refused. A candidate that was never
        ///     supplied is the ordinary case in a worker host that never sets one, so it stays silent;
        ///     reporting it would emit a line on every audit write. The rejected value itself is never
        ///     logged because it is unvalidated input that may carry control characters or a secret.
        /// </summary>
        /// <param name="source">A fixed literal naming the source that supplied the candidate.</param>
        /// <param name="reason">Why the candidate was rejected.</param>
        /// <param name="invalidIndex">The index of the first disallowed character, or -1.</param>
        /// <param name="candidateLength">The trimmed length of the candidate.</param>
        /// =================================================================================================
        private void LogRejectedCorrelationId(
            string source, CorrelationIdRejection reason, int invalidIndex, int candidateLength)
        {
            try
            {
                switch (reason)
                {
                    case CorrelationIdRejection.TooLong:
                        _logger.LogWarning(
                            "DataVigil correlation id from {CorrelationSource} was rejected: length "
                            + "{CandidateLength} exceeds the maximum of {MaxCorrelationIdLength}. Falling "
                            + "through to the next source. The rejected value is not logged because it is "
                            + "unvalidated input.",
                            source, candidateLength, AuditColumnLengths.CorrelationId);
                        break;

                    case CorrelationIdRejection.DisallowedCharacter:
                        _logger.LogWarning(
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
