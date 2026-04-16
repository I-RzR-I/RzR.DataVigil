// ***********************************************************************
//  Assembly         : RzR.DataVigil.Core
//  Author           : RzR
//  Created On       : 2026-04-11 00:04
// 
//  Last Modified By : RzR
//  Last Modified On : 2026-04-14 19:28
// ***********************************************************************
//  <copyright file="AuditRetentionService.cs" company="RzR SOFT & TECH">
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
using DomainCommonExtensions.CommonExtensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RzR.DataVigil.Abstractions.Services;
using RzR.DataVigil.Core.Options;

#endregion

namespace RzR.DataVigil.Core.Hosting
{
    /// -------------------------------------------------------------------------------------------------
    /// <summary>
    ///     Background service that periodically purges audit entries older than the configured
    ///     retention period (StorageOptions.RetentionDays). Runs once every 24 hours. 
    ///     No-op if RetentionDays is null.
    /// </summary>
    /// <seealso cref="T:Microsoft.Extensions.Hosting.BackgroundService"/>
    /// =================================================================================================
    public class AuditRetentionService : BackgroundService
    {
        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     (Immutable) the check interval.
        /// </summary>
        /// =================================================================================================
        private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(24);

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     (Immutable) the scope factory.
        /// </summary>
        /// =================================================================================================
        private readonly IServiceScopeFactory _scopeFactory;

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     (Immutable) options for controlling the storage.
        /// </summary>
        /// =================================================================================================
        private readonly StorageOptions _storageOptions;

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Initializes a new instance of the <see cref="AuditRetentionService"/> class.
        /// </summary>
        /// <param name="scopeFactory">The scope factory.</param>
        /// <param name="storageOptions">Options for controlling the storage.</param>
        /// =================================================================================================
        public AuditRetentionService(
            IServiceScopeFactory scopeFactory,
            StorageOptions storageOptions)
        {
            _scopeFactory = scopeFactory;
            _storageOptions = storageOptions;
        }

        /// <inheritdoc/>
        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            if (_storageOptions.RetentionDays.IsNull())
                return;

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await PurgeExpiredEntriesAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch
                {
                    // Swallow — retention failure should not crash the host.
                }

                try
                {
                    await Task.Delay(CheckInterval, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Purge expired entries asynchronous.
        /// </summary>
        /// <param name="cancellationToken">A token that allows processing to be cancelled.</param>
        /// <returns>
        ///     A Task.
        /// </returns>
        /// =================================================================================================
        private async Task PurgeExpiredEntriesAsync(CancellationToken cancellationToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<IAuditStore>();
            var cutoff = DateTimeOffset.UtcNow.AddDays(-_storageOptions.RetentionDays!.Value);
            await store.PurgeBeforeAsync(cutoff, cancellationToken).ConfigureAwait(false);
        }
    }
}