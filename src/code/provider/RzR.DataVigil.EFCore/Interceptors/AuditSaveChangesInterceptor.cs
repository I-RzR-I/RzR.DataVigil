// ***********************************************************************
//  Assembly         : RzR.DataVigil.EFCore
//  Author           : RzR
//  Created On       : 2026-04-10 23:04
//
//  Last Modified By : RzR
//  Last Modified On : 2026-08-19 00:00
// ***********************************************************************
//  <copyright file="AuditSaveChangesInterceptor.cs" company="RzR SOFT & TECH">
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
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using RzR.DataVigil.Abstractions.Contracts;
using RzR.DataVigil.Abstractions.Enums;
using RzR.DataVigil.Abstractions.Models.Entries;
using RzR.DataVigil.Core.Options;
using RzR.DataVigil.Core.Pipeline;
using RzR.DataVigil.EFCore.Helpers;
using RzR.DataVigil.EFCore.Models;
using RzR.Extensions.Domain.Collections;
using RzR.Extensions.Domain.Primitives;

#endregion

namespace RzR.DataVigil.EFCore.Interceptors
{
    /// -------------------------------------------------------------------------------------------------
    /// <summary>
    ///     EF Core SaveChanges interceptor for auditing Create, Update, Delete operations.
    /// </summary>
    /// <seealso cref="T:Microsoft.EntityFrameworkCore.Diagnostics.SaveChangesInterceptor"/>
    /// =================================================================================================
    public sealed class AuditSaveChangesInterceptor : SaveChangesInterceptor
    {
        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     (Immutable) the logger.
        /// </summary>
        /// =================================================================================================
        private readonly ILogger<AuditSaveChangesInterceptor> _logger;

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     (Immutable) options for controlling the operation.
        /// </summary>
        /// =================================================================================================
        private readonly AuditTrailOptions _options;

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     (Immutable) the pipeline.
        /// </summary>
        /// =================================================================================================
        private readonly AuditPipeline _pipeline;

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     (Immutable) audit transactions collected in SavingChanges and awaiting persistence in
        ///     SavedChanges, keyed per DbContext instance.
        /// </summary>
        /// =================================================================================================
        private readonly ConditionalWeakTable<DbContext, PendingAuditTransaction> _pendingTransactions
            = new ConditionalWeakTable<DbContext, PendingAuditTransaction>();

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Initializes a new instance of the <see cref="AuditSaveChangesInterceptor"/> class.
        /// </summary>
        /// <param name="options">Options for controlling the operation.</param>
        /// <param name="pipeline">The pipeline.</param>
        /// <param name="logger">The logger.</param>
        /// =================================================================================================
        public AuditSaveChangesInterceptor(AuditTrailOptions options, AuditPipeline pipeline,
            ILogger<AuditSaveChangesInterceptor> logger)
        {
            _options = options;
            _pipeline = pipeline;
            _logger = logger;
        }

        /// <inheritdoc/>
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context.IsNull())
                return await base.SavingChangesAsync(eventData, result, cancellationToken)
                    .ConfigureAwait(false);

            // Guard: never audit writes from the audit storage context itself (prevents infinite recursion)
            if (eventData.Context is AuditDbContextBase)
                return await base.SavingChangesAsync(eventData, result, cancellationToken)
                    .ConfigureAwait(false);

            // Check if context implements IAuditableContext
            var auditableCtx = eventData.Context as IAuditableContext;
            if (auditableCtx.IsNull())
                return await base.SavingChangesAsync(eventData, result, cancellationToken)
                    .ConfigureAwait(false);

            // If specific context types registered, only audit those
            if (_options.EfCore.ShouldAuditContext(eventData.Context.GetType()).IsFalse())
                return await base.SavingChangesAsync(eventData, result, cancellationToken)
                    .ConfigureAwait(false);

            // Collect while EntityState, OriginalValues and CurrentValues are still intact;
            // persist in SavedChanges, once store-generated keys are real.
            var pending = CollectTransaction(eventData.Context, auditableCtx);
            StashPending(eventData.Context, pending);

            return await base.SavingChangesAsync(eventData, result, cancellationToken)
                .ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData,
            InterceptionResult<int> result)
        {
            if (eventData.Context.IsNull())
                return base.SavingChanges(eventData, result);

            // Guard: never audit writes from the audit storage context itself (prevents infinite recursion)
            if (eventData.Context is AuditDbContextBase)
                return base.SavingChanges(eventData, result);

            var auditableCtx = eventData.Context as IAuditableContext;
            if (auditableCtx.IsNull())
                return base.SavingChanges(eventData, result);

            // If specific context types registered, only audit those
            if (_options.EfCore.ShouldAuditContext(eventData.Context.GetType()).IsFalse())
                return base.SavingChanges(eventData, result);

            var pending = CollectTransaction(eventData.Context, auditableCtx);
            StashPending(eventData.Context, pending);

            return base.SavingChanges(eventData, result);
        }

        /// <inheritdoc/>
        public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData,
            int result, CancellationToken cancellationToken = default)
        {
            var pending = TakePending(eventData.Context);
            if (pending.IsNotNull())
                try
                {
                    await PersistAsync(eventData.Context, pending, CancellationToken.None)
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Unexpected error while persisting the audit trail for context {Context}. "
                        + "The business write already completed and is not affected.",
                        eventData.Context.GetType().Name);
                }

            return await base.SavedChangesAsync(eventData, result, cancellationToken)
                .ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
        {
            var pending = TakePending(eventData.Context);
            if (pending.IsNotNull())
                try
                {
                    PersistAsync(eventData.Context, pending, CancellationToken.None)
                        .ConfigureAwait(false)
                        .GetAwaiter()
                        .GetResult();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Unexpected error while persisting the audit trail for context {Context}. "
                        + "The business write already completed and is not affected.",
                        eventData.Context.GetType().Name);
                }

            return base.SavedChanges(eventData, result);
        }

        /// <inheritdoc/>
        public override void SaveChangesFailed(DbContextErrorEventData eventData)
        {
            TakePending(eventData.Context);

            base.SaveChangesFailed(eventData);
        }

        /// <inheritdoc/>
        public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData,
            CancellationToken cancellationToken = default)
        {
            TakePending(eventData.Context);

            return base.SaveChangesFailedAsync(eventData, cancellationToken);
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Stores the collected transaction against the context, replacing any earlier one.
        ///     A null transaction clears the slot — this is what prevents a stash orphaned by a
        ///     previous save from being picked up by a later, unrelated one.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="pending">The collected transaction, or null when nothing was collected.</param>
        /// =================================================================================================
        private void StashPending(DbContext context, PendingAuditTransaction pending)
        {
            if (context.IsNull())
                return;

            if (pending.IsNotNull())
            {
                _pendingTransactions.AddOrUpdate(context, pending);

                return;
            }

            _pendingTransactions.Remove(context);
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Removes and returns the transaction collected for the given context, if any.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns>
        ///     The pending transaction, or null.
        /// </returns>
        /// =================================================================================================
        private PendingAuditTransaction TakePending(DbContext context)
        {
            if (context.IsNull())
                return null;

            if (_pendingTransactions.TryGetValue(context, out var pending).IsFalse())
                return null;

            _pendingTransactions.Remove(context);

            return pending;
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Patches store-generated values into the collected entries and runs the audit pipeline.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="pending">The pending transaction.</param>
        /// <param name="cancellationToken">A token that allows processing to be cancelled.</param>
        /// =================================================================================================
        private async Task PersistAsync(DbContext context, PendingAuditTransaction pending,
            CancellationToken cancellationToken)
        {
            PatchStoreGeneratedValues(pending, context);

            var auditResult = await _pipeline.ProcessAsync(pending.Transaction, cancellationToken)
                .ConfigureAwait(false);

            if (auditResult.IsFailure)
                _logger.LogWarning(
                    "Audit pipeline failed for context {Context}. Check AuditStore logs for details.",
                    context.GetType().Name);
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Re-reads the values that were temporary at collect time, now that the write has
        ///     completed and the database-generated values have been propagated back.
        /// </summary>
        /// <param name="pending">The pending transaction.</param>
        /// <param name="context">The context, used for diagnostics only.</param>
        /// =================================================================================================
        private void PatchStoreGeneratedValues(PendingAuditTransaction pending, DbContext context)
        {
            foreach (var pendingEntry in pending.PendingEntries)
            {
                try
                {
                    var entityEntry = pendingEntry.EntityEntry;

                    pendingEntry.AuditEntry.EntityId =
                        ChangeTrackerEntryBuilder.GetPrimaryKeyValue(entityEntry);

                    foreach (var propertyName in pendingEntry.TemporaryPropertyNames)
                    {
                        var auditProperty = pendingEntry.AuditEntry.Properties
                            .FirstOrDefault(p => p.PropertyName == propertyName);

                        if (auditProperty.IsNull())
                            continue;

                        auditProperty!.NewValue =
                            entityEntry.Property(propertyName).CurrentValue?.ToString();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Could not re-read store-generated values for {Entity} on context {Context}. "
                        + "The audit entry retains EF temporary values.",
                        pendingEntry.AuditEntry.EntityName,
                        context.GetType().Name);
                }
            }
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Collect transaction.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="auditableCtx">Context for the auditable.</param>
        /// <returns>
        ///     The collected transaction with the entries requiring a post-save value re-read, or
        ///     <c>null</c> when nothing was collected.
        /// </returns>
        /// =================================================================================================
        private PendingAuditTransaction CollectTransaction(DbContext context,
            IAuditableContext auditableCtx)
        {
            var excludedEntityTypes = new HashSet<Type>(auditableCtx.GetExcludedEntityTypes() ?? Array.Empty<Type>());

            var transactionId = Guid.NewGuid();
            var entries = new List<AuditEntry>();
            var pendingEntries = new List<PendingAuditEntry>();

            foreach (var entityEntry in context.ChangeTracker.Entries())
            {
                // Must implement IAuditable
                if ((entityEntry.Entity is IAuditable).IsFalse())
                    continue;

                // Must be CUD state
                if (entityEntry.State != EntityState.Added
                    && entityEntry.State != EntityState.Modified
                    && entityEntry.State != EntityState.Deleted)
                    continue;

                var entityType = entityEntry.Entity.GetType();

                // Global exclusions
                if (_options.GlobalExclusions.Contains(entityType))
                    continue;

                // Context-level exclusions
                if (excludedEntityTypes.Contains(entityType))
                    continue;

                // Entity-level control
                IList<string> excludedFields = null;
                if (entityEntry.Entity is IAuditableEntity auditableEntity)
                {
                    var action = ToAuditAction(entityEntry.State);
                    if (auditableEntity.ShouldAudit(action).IsFalse())
                        continue;

                    var fields = auditableEntity.GetExcludedFields();
                    if (fields.IsNotNullOrEmptyEnumerable())
                        excludedFields = new List<string>(fields);
                }

                var auditEntry = ChangeTrackerEntryBuilder.Build(entityEntry, excludedFields);
                auditEntry.TransactionId = transactionId;
                entries.Add(auditEntry);

                var isAdded = entityEntry.State == EntityState.Added;
                var temporaryPropertyNames = ChangeTrackerEntryBuilder.GetTemporaryPropertyNames(entityEntry);

                if (isAdded || temporaryPropertyNames.IsNotNullOrEmptyEnumerable())
                    pendingEntries.Add(
                        new PendingAuditEntry(auditEntry, entityEntry,
                            temporaryPropertyNames ?? Array.Empty<string>()));
            }

            if (entries.IsNullOrEmptyEnumerable())
                return null;

            var transaction = new AuditTransaction
            {
                Id = transactionId,
                Timestamp = DateTimeOffset.UtcNow,
                Entries = entries
            };

            return new PendingAuditTransaction(transaction, pendingEntries);
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Converts a state to an audit action.
        /// </summary>
        /// <param name="state">The state.</param>
        /// <returns>
        ///     State as an AuditAction.
        /// </returns>
        /// =================================================================================================
        private static AuditAction ToAuditAction(EntityState state)
        {
            switch (state)
            {
                case EntityState.Added:
                    return AuditAction.Create;
                case EntityState.Modified:
                    return AuditAction.Update;
                case EntityState.Deleted:
                    return AuditAction.Delete;
                default:
                    return AuditAction.Read;
            }
        }
    }
}
