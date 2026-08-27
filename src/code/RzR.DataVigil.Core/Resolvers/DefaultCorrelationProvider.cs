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
using RzR.DataVigil.Abstractions.Services;
using RzR.DataVigil.Core.Helpers;
using RzR.Extensions.Domain.Primitives;
using RzR.Extensions.Domain.Text;
using RzR.ResultMessage;
using RzR.ResultMessage.Abstractions;

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
        {
            _scopeContext = scopeContext;
        }

        /// <inheritdoc/>
        public IResult<string> GetCorrelationId()
        {
            var scoped = _scopeContext?.GetCurrentCorrelationId();
            if (scoped.IsNotNull() && scoped!.IsSuccess && scoped.Response.IsPresent())
                return scoped;

            return Result<string>.Success(ActivityTraceHelper.GetW3CTraceId());
        }

        /// <inheritdoc/>
        /// <remarks>
        ///     Only returns the trace id when <see cref="Activity.Current"/> uses the W3C id format and
        ///     carries a real (non-default) <see cref="ActivityTraceId"/>. 
        ///     When Activity.Current is null, or does not carry a
        ///     usable W3C trace id, this correctly returns null.
        /// </remarks>
        public IResult<string> GetTraceId()
        {
            return Result<string>.Success(ActivityTraceHelper.GetW3CTraceId());
        }
    }
}