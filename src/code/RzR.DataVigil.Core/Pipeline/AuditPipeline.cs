// ***********************************************************************
//  Assembly         : RzR.DataVigil.Core
//  Author           : RzR
//  Created On       : 2026-04-10 23:04
// 
//  Last Modified By : RzR
//  Last Modified On : 2026-04-14 19:30
// ***********************************************************************
//  <copyright file="AuditPipeline.cs" company="RzR SOFT & TECH">
//   Copyright © RzR. All rights reserved.
//  </copyright>
// 
//  <summary>
//  </summary>
// ***********************************************************************

#region U S A G E S

using System;
using System.Threading;
using System.Threading.Tasks;
using RzR.DataVigil.Abstractions.Enums;
using RzR.DataVigil.Abstractions.Models.Entries;
using RzR.DataVigil.Abstractions.Services;
using RzR.DataVigil.Core.Gdpr;
using RzR.Extensions.Domain.Collections;
using RzR.Extensions.Domain.Primitives;
using RzR.ResultMessage;
using RzR.ResultMessage.Abstractions;
using RzR.ResultMessage.Extensions.Result;

#endregion

namespace RzR.DataVigil.Core.Pipeline
{
    /// -------------------------------------------------------------------------------------------------
    /// <summary>
    ///     Core pipeline: enriches the audit transaction with user/correlation/source info, applies
    ///     GDPR storage policies to each entry, then persists via IAuditStore.
    /// </summary>
    /// =================================================================================================
    public sealed class AuditPipeline
    {
        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     (Immutable) the audit store.
        /// </summary>
        /// =================================================================================================
        private readonly IAuditStore _auditStore;

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     (Immutable) the correlation provider.
        /// </summary>
        /// =================================================================================================
        private readonly IAuditCorrelationProvider _correlationProvider;

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     (Immutable) the gdpr processor.
        /// </summary>
        /// =================================================================================================
        private readonly GdprProcessor _gdprProcessor;

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     (Immutable) source resolver.
        /// </summary>
        /// =================================================================================================
        private readonly IAuditSourceResolver _sourceResolver;

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     (Immutable) the user resolver.
        /// </summary>
        /// =================================================================================================
        private readonly IAuditUserResolver _userResolver;

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Initializes a new instance of the <see cref="AuditPipeline"/> class.
        /// </summary>
        /// <param name="userResolver">The user resolver.</param>
        /// <param name="sourceResolver">Source resolver.</param>
        /// <param name="correlationProvider">The correlation provider.</param>
        /// <param name="gdprProcessor">The gdpr processor.</param>
        /// <param name="auditStore">The audit store.</param>
        /// =================================================================================================
        public AuditPipeline(
            IAuditUserResolver userResolver,
            IAuditSourceResolver sourceResolver,
            IAuditCorrelationProvider correlationProvider,
            GdprProcessor gdprProcessor,
            IAuditStore auditStore)
        {
            _userResolver = userResolver;
            _sourceResolver = sourceResolver;
            _correlationProvider = correlationProvider;
            _gdprProcessor = gdprProcessor;
            _auditStore = auditStore;
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Process an audit transaction: enrich, apply GDPR, persist.
        /// </summary>
        /// <param name="transaction">The transaction.</param>
        /// <param name="cancellationToken">(Optional) A token that allows processing to be cancelled.</param>
        /// <returns>
        ///     The process.
        /// </returns>
        /// =================================================================================================
        public async Task<IResult> ProcessAsync(
            AuditTransaction transaction,
            CancellationToken cancellationToken = default)
        {
            if (transaction.IsNull() || transaction.Entries.IsNullOrEmptyEnumerable())
                return Result.Success();

            try
            {
                // Enrich the transaction with actor/tracing info
                var user = _userResolver.Resolve();
                var source = _sourceResolver.Resolve();
                var correlationId = _correlationProvider.GetCorrelationId();
                var traceId = _correlationProvider.GetTraceId();

                if (user.IsNotNull() && user.IsSuccess && user.Response.IsNotNull())
                {
                    transaction.UserId = user.Response.UserId;
                    transaction.UserName = user.Response.UserName;
                    transaction.IpAddress = user.Response.IpAddress;
                }

                transaction.Source = source.Response;
                transaction.CorrelationId = correlationId.Response;
                transaction.TraceId = traceId.Response;

                // Apply GDPR storage policies to each entry
                var anyGdprApplied = false;
                var allFullyAnonymized = true;
                foreach (var entry in transaction.Entries.NotNull())
                {
                    var (_, applied, fullyAnonymized) = _gdprProcessor.ApplyStoragePolicies(entry);
                    if (applied.IsTrue())
                    {
                        anyGdprApplied = true;
                        if (fullyAnonymized.IsFalse())
                            allFullyAnonymized = false;
                    }
                }

                if (anyGdprApplied.IsTrue())
                    transaction.GdprState = allFullyAnonymized
                        ? GdprStorageState.FullyAnonymized
                        : GdprStorageState.PartiallyProcessed;

                return await _auditStore.SaveAsync(transaction, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                return Result
                    .Failure(ex.Message)
                    .WithError(ex);
            }
        }
    }
}