// ***********************************************************************
//  Assembly         : RzR.DataVigil.Storage.File
//  Author           : RzR
//  Created On       : 2026-04-10 23:04
// 
//  Last Modified By : RzR
//  Last Modified On : 2026-08-18 21:54
// ***********************************************************************
//  <copyright file="FileAuditStore.cs" company="RzR SOFT & TECH">
//   Copyright © RzR. All rights reserved.
//  </copyright>
// 
//  <summary>
//  </summary>
// ***********************************************************************

#region U S A G E S

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RzR.DataVigil.Abstractions.Constants;
using RzR.DataVigil.Abstractions.Enums;
using RzR.DataVigil.Abstractions.Extensions;
using RzR.DataVigil.Abstractions.Models.Entries;
using RzR.DataVigil.Abstractions.Models.Gdpr;
using RzR.DataVigil.Abstractions.Models.Query;
using RzR.DataVigil.Abstractions.Services;
using RzR.DataVigil.Core.Extensions;
using RzR.DataVigil.Core.Gdpr;
using RzR.DataVigil.Core.Helpers;
using RzR.DataVigil.Core.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using RzR.Extensions.Domain.Collections;
using RzR.Extensions.Domain.Primitives;
using RzR.Extensions.Domain.Reflection.TypeParam;
using RzR.Extensions.Domain.Text;
using RzR.ResultMessage;
using RzR.ResultMessage.Abstractions;
using RzR.ResultMessage.Extensions.Result;

#endregion

namespace RzR.DataVigil.Storage.File
{
    /// -------------------------------------------------------------------------------------------------
    /// <summary>
    ///     File-based implementation of IAuditStore. Stores audit transactions as JSON files (one
    ///     file per day).
    /// </summary>
    /// <seealso cref="T:RzR.DataVigil.Abstractions.Services.IAuditStore"/>
    /// =================================================================================================
    public class FileAuditStore : IAuditStore
    {
        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     (Immutable) the gdpr processor.
        /// </summary>
        /// =================================================================================================
        private readonly GdprProcessor _gdprProcessor;

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     (Immutable) options for controlling the JSON.
        /// </summary>
        /// =================================================================================================
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     (Immutable) options for controlling the operation.
        /// </summary>
        /// =================================================================================================
        private readonly StorageOptions _options;

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     (Immutable) the write lock.
        /// </summary>
        /// =================================================================================================
        private readonly object _writeLock = new object();

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     (Immutable) the logger, never null.
        /// </summary>
        /// =================================================================================================
        private readonly ILogger<FileAuditStore> _logger;

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Initializes a new instance of the <see cref="FileAuditStore"/> class.
        /// </summary>
        /// <param name="options">Options for controlling the operation.</param>
        /// <param name="gdprProcessor">(Immutable) the gdpr processor.</param>
        /// =================================================================================================
        public FileAuditStore(StorageOptions options, GdprProcessor gdprProcessor)
            : this(options, gdprProcessor, logger: null)
        {
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Initializes a new instance of the <see cref="FileAuditStore"/> class.
        /// </summary>
        /// <param name="options">Options for controlling the operation.</param>
        /// <param name="gdprProcessor">(Immutable) the gdpr processor.</param>
        /// <param name="logger">
        ///     The logger used to report a capped page size, may be null. Without it a caller who asks for
        ///     more records than the store will serve gets a short page and no signal at all, which reads
        ///     as "there are no more records" - the one way an audit export must never mislead.
        /// </param>
        /// =================================================================================================
        public FileAuditStore(StorageOptions options, GdprProcessor gdprProcessor,
            ILogger<FileAuditStore> logger)
        {
            _options = options;
            _gdprProcessor = gdprProcessor;
            _logger = logger ?? NullLogger<FileAuditStore>.Instance;

            if (_options.FilePath.IsPresent())
                Directory.CreateDirectory(_options.FilePath);
        }

        /// <inheritdoc/>
        public Task<IResult> SaveAsync(AuditTransaction transaction, CancellationToken cancellationToken = default)
        {
            if (transaction.IsNull())
                return Task.FromResult<IResult>(Result.Success());

            try
            {
                var basePath = GetBasePath();
                var dateKey = transaction.Timestamp.UtcDateTime.ToString("yyyy-MM-dd");

                lock (_writeLock)
                {
                    var filePath = Path.Combine(basePath, $"audit-{dateKey}.json");

                    List<AuditTransaction> existing;
                    if (System.IO.File.Exists(filePath))
                    {
                        var json = System.IO.File.ReadAllText(filePath);
                        existing = JsonSerializer.Deserialize<List<AuditTransaction>>(json) ??
                                   new List<AuditTransaction>();
                    }
                    else
                    {
                        existing = new List<AuditTransaction>();
                    }

                    existing.Add(transaction);

                    var output = JsonSerializer.Serialize(existing, JsonOptions);
                    System.IO.File.WriteAllText(filePath, output);
                }

                return Task.FromResult<IResult>(Result.Success());
            }
            catch (Exception ex)
            {
                return Task.FromResult<IResult>(Result.Failure(ex.Message));
            }
        }

        /// <inheritdoc />
        public async Task<IResult<IEnumerable<AuditTransaction>>> QueryAsync(AuditTransactionQuery filters,
            GdprRetrievalContext gdprRetrievalContext = null, CancellationToken cancellationToken = default)
        {
            try
            {
                filters = filters.IfIsNull(new AuditTransactionQuery());

                filters.GetEffectivePaging(out var skip, out var take, out var pagingWasNormalized,
                    out var takeWasCapped);

                if (takeWasCapped)
                    _logger.LogWarning(
                        "Audit query requested Take={RequestedTake}, which exceeds the maximum page size " +
                        "of {MaxTake}; the result was capped. A short page does not mean there are no " +
                        "further records - use Skip to page through the rest.",
                        filters.Take, AuditQueryLimits.MaxTake);

                if (pagingWasNormalized && _logger.IsEnabled(LogLevel.Debug))
                    _logger.LogDebug(
                        "Audit query paging was normalized to Skip={Skip}, Take={Take}.", skip, take);

                if (take == 0)
                    return Result<IEnumerable<AuditTransaction>>.Success(new List<AuditTransaction>());

                gdprRetrievalContext = gdprRetrievalContext.IfIsNull(new GdprRetrievalContext());

                var basePath = GetBasePath();
                var allTransactions = new List<AuditTransaction>();

                if (Directory.Exists(basePath))
                {
                    foreach (var file in Directory.GetFiles(basePath, "audit-*.json"))
                    {
                        var json = await System.IO.File.ReadAllTextAsync(file, cancellationToken);
                        var transactions = JsonSerializer.Deserialize<List<AuditTransaction>>(json);

                        if (transactions.IsNotNull())
                            allTransactions.AddRange(transactions);
                    }
                }

                var resultList = ApplyFilters(allTransactions, filters)
                    .OrderByDescending(x => x.Timestamp)
                    .ThenByDescending(x => x.Id)
                    .Skip(skip)
                    .Take(take)
                    .ToList();

                foreach (var txn in resultList.NotNull())
                {
                    foreach (var entry in txn.Entries.NotNull())
                    {
                        if (_gdprProcessor.IsNotNull())
                            _gdprProcessor.ApplyRetrievalPolicies(entry, gdprRetrievalContext);
                    }
                }

                return Result<IEnumerable<AuditTransaction>>.Success(resultList);

            }
            catch (Exception ex)
            {
                return Result<IEnumerable<AuditTransaction>>.Failure()
                    .WithError(ex);
            }
        }

        /// <inheritdoc/>
        public Task<IResult> AnonymizeByUserAsync(string userId, CancellationToken cancellationToken = default)
        {
            try
            {
                userId = AuditColumnValue.TruncateToColumnLength(userId, AuditColumnLengths.UserId);

                var basePath = GetBasePath();

                if (Directory.Exists(basePath).IsFalse())
                    return Task.FromResult<IResult>(Result.Success());

                lock (_writeLock)
                {
                    foreach (var file in Directory.GetFiles(basePath, "audit-*.json"))
                    {
                        var json = System.IO.File.ReadAllText(file);
                        var transactions = JsonSerializer.Deserialize<List<AuditTransaction>>(json);
                        if (transactions == null)
                            continue;

                        var modified = false;
                        foreach (var transaction in transactions)
                        {
                            if (transaction.UserId == userId)
                            {
                                transaction.UserId = transaction.UserId.AsErased();
                                transaction.UserName = transaction.UserName.AsErased();
                                transaction.IpAddress = transaction.IpAddress.AsErased();
                                transaction.GdprState = GdprStorageState.Erased;
                                modified = true;
                            }
                        }

                        if (modified.IsTrue())
                        {
                            var output = JsonSerializer.Serialize(transactions, JsonOptions);
                            System.IO.File.WriteAllText(file, output);
                        }
                    }
                }

                return Task.FromResult<IResult>(Result.Success());
            }
            catch (Exception ex)
            {
                return Task.FromResult<IResult>(Result.Failure(ex.Message));
            }
        }

        /// <inheritdoc/>
        public Task<IResult> PurgeBeforeAsync(DateTimeOffset before, CancellationToken cancellationToken = default)
        {
            try
            {
                var basePath = GetBasePath();

                if (!Directory.Exists(basePath))
                    return Task.FromResult<IResult>(Result.Success());

                lock (_writeLock)
                {
                    foreach (var file in Directory.GetFiles(basePath, "audit-*.json"))
                    {
                        var json = System.IO.File.ReadAllText(file);
                        var transactions = JsonSerializer.Deserialize<List<AuditTransaction>>(json);
                        if (transactions == null)
                            continue;

                        var remaining = transactions.Where(t => t.Timestamp >= before).ToList();

                        if (remaining.Count == 0)
                        {
                            System.IO.File.Delete(file);
                        }
                        else if (remaining.Count < transactions.Count)
                        {
                            var output = JsonSerializer.Serialize(remaining, JsonOptions);
                            System.IO.File.WriteAllText(file, output);
                        }
                    }
                }

                return Task.FromResult<IResult>(Result.Success());
            }
            catch (Exception ex)
            {
                return Task.FromResult<IResult>(Result.Failure(ex.Message));
            }
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Gets base path.
        /// </summary>
        /// <returns>
        ///     The base path.
        /// </returns>
        /// =================================================================================================
        private string GetBasePath()
        {
            return _options.FilePath.IsPresent()
                ? _options.FilePath
                : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "audit-logs");
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Composes the optional filter predicates onto the query, combined with AND. A filter left
        ///     at its default contributes no predicate at all, so an unfiltered query object still
        ///     produces the result set this store returned before filtering existed.
        /// </summary>
        /// <param name="source">The query to compose the predicates onto.</param>
        /// <param name="filters">The filters supplied by the caller.</param>
        /// <returns>
        ///     The query with every supplied filter applied.
        /// </returns>
        /// =================================================================================================
        private static IEnumerable<AuditTransaction> ApplyFilters(IEnumerable<AuditTransaction> source,
            AuditTransactionQuery filters)
        {
            if (filters.FromUtc.HasValue)
            {
                var fromUtc = filters.FromUtc.Value;
                source = source.Where(x => x.Timestamp >= fromUtc);
            }

            if (filters.ToUtc.HasValue)
            {
                var toUtc = filters.ToUtc.Value;
                source = source.Where(x => x.Timestamp < toUtc);
            }

            if (filters.UserId.IsPresent())
            {
                var userId = filters.UserId;
                source = source.Where(x => x.UserId == userId);
            }

            if (filters.CorrelationId.IsPresent())
            {
                var correlationId = filters.CorrelationId;
                source = source.Where(x => x.CorrelationId == correlationId);
            }

            if (filters.GdprState.HasValue)
            {
                var gdprState = filters.GdprState.Value;
                source = source.Where(x => x.GdprState == gdprState);
            }

            return source;
        }
    }
}