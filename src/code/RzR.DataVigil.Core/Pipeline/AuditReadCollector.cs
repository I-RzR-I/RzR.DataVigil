// ***********************************************************************
//  Assembly         : RzR.DataVigil.Core
//  Author           : RzR
//  Created On       : 2026-04-15 18:04
// 
//  Last Modified By : RzR
//  Last Modified On : 2026-04-15 18:04
// ***********************************************************************
//  <copyright file="AuditReadCollector.cs" company="RzR SOFT & TECH">
//   Copyright © RzR. All rights reserved.
//  </copyright>
// 
//  <summary>
//  </summary>
// ***********************************************************************

#region U S A G E S

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RzR.DataVigil.Abstractions.Models.Entries;
using RzR.Extensions.Domain.Primitives;

#endregion

namespace RzR.DataVigil.Core.Pipeline
{
    /// -------------------------------------------------------------------------------------------------
    /// <summary>
    ///     Scoped collector that accumulates Read audit entries during a request
    ///     and flushes them through the <see cref="AuditPipeline"/> in a single batch.
    ///     <para>
    ///     Entries are added synchronously (e.g. from a materialization interceptor)
    ///     and flushed asynchronously at the end of the request by middleware.
    ///     </para>
    /// </summary>
    /// =================================================================================================
    public sealed class AuditReadCollector
    {
        private readonly AuditPipeline _pipeline;
        private readonly ILogger<AuditReadCollector> _logger;
        private readonly List<AuditEntry> _entries = new List<AuditEntry>();
        private readonly object _lock = new object();

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Initializes a new instance of the <see cref="AuditReadCollector"/> class.
        /// </summary>
        /// <param name="pipeline">The audit pipeline.</param>
        /// <param name="logger">The logger.</param>
        /// =================================================================================================
        public AuditReadCollector(AuditPipeline pipeline, ILogger<AuditReadCollector> logger)
        {
            _pipeline = pipeline;
            _logger = logger;
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Gets a value indicating whether the collector has pending entries.
        /// </summary>
        /// =================================================================================================
        public bool HasEntries
        {
            get
            {
                lock (_lock) { return _entries.Count > 0; }
            }
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Adds a Read audit entry to the collector.
        ///     This method is thread-safe and may be called from synchronous interceptor callbacks.
        /// </summary>
        /// <param name="entry">The audit entry to collect.</param>
        /// =================================================================================================
        public void Collect(AuditEntry entry)
        {
            if (entry.IsNull()) return;

            lock (_lock) { _entries.Add(entry); }
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Flushes all collected entries through the audit pipeline as a single transaction.
        ///     After flushing, the internal buffer is cleared.
        /// </summary>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A Task representing the async operation.</returns>
        /// =================================================================================================
        public async Task FlushAsync(CancellationToken cancellationToken = default)
        {
            List<AuditEntry> snapshot;

            lock (_lock)
            {
                if (_entries.Count == 0)
                    return;

                snapshot = new List<AuditEntry>(_entries);
                _entries.Clear();
            }

            try
            {
                var transaction = new AuditTransaction
                {
                    Id = Guid.NewGuid(),
                    Timestamp = DateTimeOffset.UtcNow,
                    Entries = snapshot
                };

                var result = await _pipeline.ProcessAsync(transaction, cancellationToken)
                    .ConfigureAwait(false);

                if (result.IsFailure)
                    _logger.LogWarning("Audit pipeline failed while flushing {Count} Read entries.", snapshot.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to flush {Count} Read audit entries.", snapshot.Count);
            }
        }
    }
}
