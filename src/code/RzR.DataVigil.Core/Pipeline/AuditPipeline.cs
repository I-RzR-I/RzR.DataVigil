// ***********************************************************************
//  Assembly         : RzR.DataVigil.Core
//  Author           : RzR
//  Created On       : 2026-04-10 23:04
// 
//  Last Modified By : RzR
//  Last Modified On : 2026-08-28 20:40
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
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RzR.DataVigil.Abstractions.Constants;
using RzR.DataVigil.Abstractions.Enums;
using RzR.DataVigil.Abstractions.Models.Entries;
using RzR.DataVigil.Abstractions.Services;
using RzR.DataVigil.Core.Gdpr;
using RzR.Extensions.Domain.Collections;
using RzR.Extensions.Domain.Primitives;
using RzR.Extensions.Domain.Text;
using RzR.ResultMessage;
using RzR.ResultMessage.Abstractions;
using RzR.ResultMessage.Extensions.Result;

#endregion

namespace RzR.DataVigil.Core.Pipeline
{
    /// -------------------------------------------------------------------------------------------------
    /// <summary>
    ///     Core pipeline: enriches the audit transaction with user/correlation/source info, records
    ///     how the actor was determined (see <see cref="T:RzR.DataVigil.Abstractions.Enums.AuditUserSource"/>)
    ///     in Metadata, applies GDPR storage policies to each entry, then persists via IAuditStore.
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
        ///     (Immutable) the metadata enrichers, never null.
        /// </summary>
        /// =================================================================================================
        private readonly IAuditMetadataEnricher[] _metadataEnrichers;

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     (Immutable) the logger, never null.
        /// </summary>
        /// =================================================================================================
        private readonly ILogger<AuditPipeline> _logger;

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Initializes a new instance of the <see cref="AuditPipeline"/> class without metadata
        ///     enrichers.
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
            : this(userResolver, sourceResolver, correlationProvider, gdprProcessor, auditStore, metadataEnrichers: null)
        {
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Initializes a new instance of the <see cref="AuditPipeline"/> class without a logger.
        /// </summary>
        /// <param name="userResolver">The user resolver.</param>
        /// <param name="sourceResolver">Source resolver.</param>
        /// <param name="correlationProvider">The correlation provider.</param>
        /// <param name="gdprProcessor">The gdpr processor.</param>
        /// <param name="auditStore">The audit store.</param>
        /// <param name="metadataEnrichers">The metadata enrichers, may be null or empty.</param>
        /// =================================================================================================
        public AuditPipeline(
            IAuditUserResolver userResolver,
            IAuditSourceResolver sourceResolver,
            IAuditCorrelationProvider correlationProvider,
            GdprProcessor gdprProcessor,
            IAuditStore auditStore,
            IEnumerable<IAuditMetadataEnricher> metadataEnrichers)
            : this(userResolver, sourceResolver, correlationProvider, gdprProcessor, auditStore,
                metadataEnrichers, logger: null)
        {
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Initializes a new instance of the <see cref="AuditPipeline"/> class.
        /// </summary>
        /// <param name="userResolver">The user resolver.</param>
        /// <param name="sourceResolver">Source resolver.</param>
        /// <param name="correlationProvider">The correlation provider.</param>
        /// <param name="gdprProcessor">The gdpr processor.</param>
        /// <param name="auditStore">The audit store.</param>
        /// <param name="metadataEnrichers">The metadata enrichers, may be null or empty.</param>
        /// <param name="logger">The logger, may be null.</param>
        /// =================================================================================================
        public AuditPipeline(
            IAuditUserResolver userResolver,
            IAuditSourceResolver sourceResolver,
            IAuditCorrelationProvider correlationProvider,
            GdprProcessor gdprProcessor,
            IAuditStore auditStore,
            IEnumerable<IAuditMetadataEnricher> metadataEnrichers,
            ILogger<AuditPipeline> logger)
        {
            _userResolver = userResolver;
            _sourceResolver = sourceResolver;
            _correlationProvider = correlationProvider;
            _gdprProcessor = gdprProcessor;
            _auditStore = auditStore;
            _metadataEnrichers = metadataEnrichers.NotNull().ToArray();
            _logger = logger ?? NullLogger<AuditPipeline>.Instance;
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
        public async Task<IResult> ProcessAsync(AuditTransaction transaction,
            CancellationToken cancellationToken = default)
        {
            if (transaction.IsNull() || transaction.Entries.IsNullOrEmptyEnumerable())
                return Result.Success();

            try
            {
                var user = _userResolver.Resolve();
                var source = _sourceResolver.Resolve();
                var correlationId = _correlationProvider.GetCorrelationId();
                var traceId = _correlationProvider.GetTraceId();

                AuditUserSource userSource;
                if (user.IsNull() || user.IsSuccess.IsFalse())
                {
                    userSource = AuditUserSource.Unresolved;
                }
                else if (user.Response.IsNull())
                {
                    userSource = AuditUserSource.Anonymous;
                }
                else
                {
                    transaction.UserId = user.Response.UserId;
                    transaction.UserName = user.Response.UserName;
                    transaction.IpAddress = user.Response.IpAddress;
                    userSource = user.Response.Source;
                }

                if (transaction.Metadata.IsNull())
                    transaction.Metadata = new Dictionary<string, string>();

                ApplyMetadataEnrichers(transaction.Metadata);

                transaction.Metadata[AuditMetadataKeys.UserSource] = userSource.ToString();

                transaction.Source = ValueOrNull(source);
                transaction.CorrelationId = ValueOrNull(correlationId);
                transaction.TraceId = ValueOrNull(traceId);

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

                return await _auditStore.SaveAsync(transaction, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException ex)
            {
                return Result
                    .Failure(ex.Message)
                    .WithError(ex);
            }
            catch (Exception ex)
            {
                LogProcessFailure(ex);

                return Result
                    .Failure(ex.Message)
                    .WithError(ex);
            }
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Runs every registered enricher and copies the pairs it returns into the metadata
        ///     dictionary.
        /// </summary>
        /// <param name="metadata">The metadata dictionary to write into.</param>
        /// =================================================================================================
        private void ApplyMetadataEnrichers(IDictionary<string, string> metadata)
        {
            foreach (var enricher in _metadataEnrichers)
            {
                try
                {
                    var enriched = enricher.Enrich();
                    if (enriched.IsNull() || enriched.IsSuccess.IsFalse() || enriched.Response.IsNull())
                        continue;

                    foreach (var pair in enriched.Response)
                    {
                        if (pair.Key.IsPresent())
                            metadata[pair.Key] = pair.Value;
                    }
                }
                catch (Exception ex)
                {
                    LogEnricherFailure(enricher, ex);
                }
            }
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Reports that a single enricher threw and was skipped, without ever letting the report
        ///     itself break the audit write.
        /// </summary>
        /// <param name="enricher">The enricher that threw, may be null.</param>
        /// <param name="ex">The exception thrown by the enricher.</param>
        /// =================================================================================================
        private void LogEnricherFailure(IAuditMetadataEnricher enricher, Exception ex)
        {
            try
            {
                var enricherType = enricher?.GetType().FullName ?? "<null>";

                _logger.LogWarning(ex,
                    "DataVigil metadata enricher {EnricherType} threw and was skipped. " +
                    "The audit record is still written, without that enricher's metadata.",
                    enricherType);
            }
            catch
            {
                /* logging must never break the audit write */
            }
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Reports that the pipeline failed to process a transaction, without ever letting the report
        ///     itself replace the failure that is being returned to the caller.
        /// </summary>
        /// <param name="ex">The exception that ended the processing.</param>
        /// =================================================================================================
        private void LogProcessFailure(Exception ex)
        {
            try
            {
                _logger.LogError(ex,
                    "DataVigil audit pipeline failed to process the transaction and no audit record " +
                    "was written. The transaction content is not logged.");
            }
            catch
            {
                /* logging must never break the audit write */
            }
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Returns the payload of a resolver result, or null when the result is missing or failed.
        ///     Reading Response directly would silently store the payload of a failed lookup, and would
        ///     throw for a third-party provider that returns a null result.
        /// </summary>
        /// <param name="result">The resolver result.</param>
        /// <returns>
        ///     The resolved value, or null.
        /// </returns>
        /// =================================================================================================
        private static string ValueOrNull(IResult<string> result)
            => result.IsNull() || result.IsSuccess.IsFalse() ? null : result.Response;
    }
}