// ***********************************************************************
//  Assembly         : RzR.DataVigil.Storage.EfMongoDb
//  Author           : RzR
//  Created On       : 2026-04-15 11:04
// 
//  Last Modified By : RzR
//  Last Modified On : 2026-04-15 12:03
// ***********************************************************************
//  <copyright file="MongoDbAuditStore.cs" company="RzR SOFT & TECH">
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
using AggregatedGenericResultMessage;
using AggregatedGenericResultMessage.Abstractions;
using AggregatedGenericResultMessage.Extensions.Result;
using DomainCommonExtensions.ArraysExtensions;
using DomainCommonExtensions.CommonExtensions;
using DomainCommonExtensions.CommonExtensions.TypeParam;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RzR.DataVigil.Abstractions.Enums;
using RzR.DataVigil.Abstractions.Models.Entries;
using RzR.DataVigil.Abstractions.Models.Gdpr;
using RzR.DataVigil.Abstractions.Models.Query;
using RzR.DataVigil.Abstractions.Services;
using RzR.DataVigil.Core.Extensions;
using RzR.DataVigil.Core.Gdpr;

#endregion

namespace RzR.DataVigil.Storage.EfMongoDb
{
    /// -------------------------------------------------------------------------------------------------
    /// <summary>
    ///     MongoDB implementation of <see cref="IAuditStore" /> using EF Core.
    ///     Persists audit transactions to a MongoDB database and supports pagination,
    ///     GDPR retrieval policies, user anonymization, and time-based purging.
    /// </summary>
    /// <seealso cref="T:RzR.DataVigil.Abstractions.Services.IAuditStore" />
    /// =================================================================================================
    public class MongoDbAuditStore : IAuditStore
    {
        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     (Immutable) the MongoDB audit database context.
        /// </summary>
        /// =================================================================================================
        private readonly AuditMongoDbContext _dbContext;

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     (Immutable) the GDPR processor for applying retrieval-time policies.
        /// </summary>
        /// =================================================================================================
        private readonly GdprProcessor _gdprProcessor;

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     (Immutable) the logger instance.
        /// </summary>
        /// =================================================================================================
        private readonly ILogger<MongoDbAuditStore> _logger;

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Initializes a new instance of the <see cref="MongoDbAuditStore" /> class.
        /// </summary>
        /// <param name="dbContext">The MongoDB audit database context.</param>
        /// <param name="logger">The logger instance for diagnostic output.</param>
        /// <param name="gdprProcessor">The GDPR processor for retrieval-time field policies.</param>
        /// =================================================================================================
        public MongoDbAuditStore(
            AuditMongoDbContext dbContext,
            ILogger<MongoDbAuditStore> logger,
            GdprProcessor gdprProcessor)
        {
            _dbContext = dbContext;
            _logger = logger;
            _gdprProcessor = gdprProcessor;
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Persists an audit transaction to the MongoDB database.
        ///     Returns success if the transaction is null (no-op).
        /// </summary>
        /// <param name="transaction">The audit transaction to persist, or null for a no-op.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>
        ///     An <see cref="IResult" /> indicating success or failure.
        /// </returns>
        /// =================================================================================================
        public async Task<IResult> SaveAsync(AuditTransaction transaction,
            CancellationToken cancellationToken = default)
        {
            if (transaction.IsNull())
                return Result.Success();

            try
            {
                _dbContext.AuditTransactions.Add(transaction);
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                return Result.Success();
            }
            catch (Exception ex)
            {
                return Result.Failure(ex.Message);
            }
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Queries audit transactions with pagination and applies GDPR retrieval policies
        ///     to each entry before returning the results.
        /// </summary>
        /// <param name="filters">Pagination parameters (skip/take).</param>
        /// <param name="gdprRetrievalContext">GDPR context with user roles/claims for field-level access control.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>
        ///     An <see cref="IResult{T}" /> containing the matching audit transactions.
        /// </returns>
        /// =================================================================================================
        public async Task<IResult<IEnumerable<AuditTransaction>>> QueryAsync(AuditTransactionQuery filters,
            GdprRetrievalContext gdprRetrievalContext = null, CancellationToken cancellationToken = default)
        {
            try
            {
                filters = filters.IfIsNull(new AuditTransactionQuery());
                gdprRetrievalContext = gdprRetrievalContext.IfIsNull(new GdprRetrievalContext());

                var query = await _dbContext.AuditTransactions
                    .AsNoTracking()
                    .OrderByDescending(x => x.Timestamp)
                    .Skip(filters.Skip)
                    .Take(filters.Take)
                    .ToListAsync(cancellationToken);

                foreach (var txn in query.NotNull())
                    foreach (var entry in txn.Entries.NotNull())
                        if (_gdprProcessor.IsNotNull())
                            _gdprProcessor.ApplyRetrievalPolicies(entry, gdprRetrievalContext);

                return Result<IEnumerable<AuditTransaction>>.Success(query);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while trying to get audit log query!");

                return Result<IEnumerable<AuditTransaction>>.Failure()
                    .WithError(ex);
            }
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Anonymizes all audit transactions belonging to a specific user by replacing
        ///     personal identifiers (UserId, UserName, IpAddress) with "[ERASED]" markers
        ///     and setting the GDPR state to <see cref="GdprStorageState.Erased" />.
        /// </summary>
        /// <param name="userId">The user identifier whose audit data should be anonymized.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>
        ///     An <see cref="IResult" /> indicating success or failure.
        /// </returns>
        /// =================================================================================================
        public async Task<IResult> AnonymizeByUserAsync(string userId, CancellationToken cancellationToken = default)
        {
            try
            {
                var transactions = await _dbContext.AuditTransactions
                    .Where(t => t.UserId == userId)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                foreach (var transaction in transactions)
                {
                    transaction.UserId = transaction.UserId.AsErased();
                    transaction.UserName = transaction.UserName.AsErased();
                    transaction.IpAddress = transaction.IpAddress.AsErased();
                    transaction.GdprState = GdprStorageState.Erased;
                }

                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                return Result.Success();
            }
            catch (Exception ex)
            {
                return Result.Failure(ex.Message);
            }
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Permanently deletes all audit transactions (including entries and properties)
        ///     with a timestamp older than the specified cutoff date.
        /// </summary>
        /// <param name="before">The cutoff date; transactions older than this are deleted.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>
        ///     An <see cref="IResult" /> indicating success or failure.
        /// </returns>
        /// =================================================================================================
        public async Task<IResult> PurgeBeforeAsync(DateTimeOffset before,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var transactions = await _dbContext.AuditTransactions
                    .Where(t => t.Timestamp < before)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                _dbContext.AuditTransactions.RemoveRange(transactions);
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                return Result.Success();
            }
            catch (Exception ex)
            {
                return Result.Failure(ex.Message);
            }
        }
    }
}