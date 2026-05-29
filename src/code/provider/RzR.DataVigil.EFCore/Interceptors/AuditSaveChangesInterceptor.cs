// ***********************************************************************
//  Assembly         : RzR.DataVigil.EFCore
//  Author           : RzR
//  Created On       : 2026-04-10 23:04
// 
//  Last Modified By : RzR
//  Last Modified On : 2026-04-14 18:16
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
        ///     (Immutable) the entry builder.
        /// </summary>
        /// =================================================================================================
        private readonly ChangeTrackerEntryBuilder _entryBuilder;

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
        ///     Initializes a new instance of the <see cref="AuditSaveChangesInterceptor"/> class.
        /// </summary>
        /// <param name="options">Options for controlling the operation.</param>
        /// <param name="pipeline">The pipeline.</param>
        /// <param name="logger">The logger.</param>
        /// =================================================================================================
        public AuditSaveChangesInterceptor(
            AuditTrailOptions options,
            AuditPipeline pipeline,
            ILogger<AuditSaveChangesInterceptor> logger)
        {
            _options = options;
            _pipeline = pipeline;
            _entryBuilder = new ChangeTrackerEntryBuilder();
            _logger = logger;
        }

        /// <inheritdoc/>
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
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

            var transaction = CollectTransaction(eventData.Context, auditableCtx);
            if (transaction.IsNotNull())
            {
                var auditResult = await _pipeline.ProcessAsync(transaction, cancellationToken).ConfigureAwait(false);
                if (auditResult.IsFailure)
                    _logger.LogWarning(
                        "Audit pipeline failed for context {Context}. Check AuditStore logs for details.",
                        eventData.Context.GetType().Name);
            }

            return await base.SavingChangesAsync(eventData, result, cancellationToken)
                .ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public override InterceptionResult<int> SavingChanges(
            DbContextEventData eventData,
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

            var transaction = CollectTransaction(eventData.Context, auditableCtx);
            if (transaction.IsNotNull())
            {
                var auditResult = _pipeline.ProcessAsync(transaction, CancellationToken.None)
                    .ConfigureAwait(false)
                    .GetAwaiter()
                    .GetResult();

                if (auditResult.IsFailure)
                    _logger.LogWarning(
                        "Audit pipeline failed for context {Context}. Check AuditStore logs for details.",
                        eventData.Context.GetType().Name);
            }

            return base.SavingChanges(eventData, result);
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Collect transaction.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="auditableCtx">Context for the auditable.</param>
        /// <returns>
        ///     An AuditTransaction.
        /// </returns>
        /// =================================================================================================
        private AuditTransaction CollectTransaction(
            DbContext context,
            IAuditableContext auditableCtx)
        {
            var excludedEntityTypes = new HashSet<Type>(auditableCtx.GetExcludedEntityTypes() ?? Array.Empty<Type>());

            var transactionId = Guid.NewGuid();
            var entries = new List<AuditEntry>();

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
            }

            if (entries.IsNullOrEmptyEnumerable())
                return null;

            return new AuditTransaction
            {
                Id = transactionId,
                Timestamp = DateTimeOffset.UtcNow,
                Entries = entries
            };
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