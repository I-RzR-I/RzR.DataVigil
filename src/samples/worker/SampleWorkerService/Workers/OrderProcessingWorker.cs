// ***********************************************************************
//  Assembly         : RzR.DataVigil.SampleWorkerService
//  Author           : RzR
//  Created On       : 2026-04-15 21:04
// 
//  Last Modified By : RzR
//  Last Modified On : 2026-04-15 22:06
// ***********************************************************************
//  <copyright file="OrderProcessingWorker.cs" company="RzR SOFT & TECH">
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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RzR.DataVigil.Abstractions.Enums;
using RzR.DataVigil.Abstractions.Models.Entries;
using RzR.DataVigil.Abstractions.Models.Identity;
using RzR.DataVigil.Abstractions.Services;
using RzR.DataVigil.Core.Pipeline;
using SampleWorkerService.Models;

#endregion

namespace SampleWorkerService.Workers
{
    /// <summary>
    ///     Background job that processes orders and creates audit entries
    ///     using the manual audit scope (no HttpContext available).
    /// </summary>
    public class OrderProcessingWorker : BackgroundService
    {
        private readonly ILogger<OrderProcessingWorker> _logger;
        private readonly IServiceScopeFactory _scopeFactory;

        public OrderProcessingWorker(
            IServiceScopeFactory scopeFactory,
            ILogger<OrderProcessingWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("OrderProcessingWorker started.");

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessOrderBatchAsync(cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing order batch.");
                }

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            _logger.LogInformation("OrderProcessingWorker stopped.");
        }

        /// <summary>
        ///     Simulates processing an order batch. Demonstrates creating a DI scope,
        ///     setting the audit user via IAuditScopeContext, and pushing a manual
        ///     AuditTransaction through the AuditPipeline.
        /// </summary>
        private async Task ProcessOrderBatchAsync(CancellationToken cancellationToken)
        {
            // Each batch runs in its own DI scope so IAuditScopeContext is isolated.
            using var scope = _scopeFactory.CreateScope();
            var sp = scope.ServiceProvider;

            //     In a worker there is no HttpContext, so we tell the audit system
            //     who the actor is by writing to IAuditScopeContext.
            var scopeContext = sp.GetRequiredService<IAuditScopeContext>();
            scopeContext.SetUser(new AuditUserInfo
            {
                UserId = "worker-order-processor",
                UserName = "OrderProcessingWorker",
                IpAddress = "127.0.0.1"
            });

            // When you don't use EF Core interceptors (or your writes happen
            // outside EF), you can push audit entries through the pipeline yourself.
            var transaction = new AuditTransaction
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTimeOffset.UtcNow,
                Entries = new List<AuditEntry>
                {
                    new AuditEntry
                    {
                        Id = Guid.NewGuid(),
                        EntityName = nameof(Order),
                        EntityId = "42",
                        Action = AuditAction.Update,
                        Properties = new List<AuditEntryProperty>
                        {
                            new AuditEntryProperty
                            {
                                PropertyName = "Status",
                                PropertyType = "System.String",
                                OldValue = "Pending",
                                NewValue = "Shipped"
                            },
                            new AuditEntryProperty
                            {
                                PropertyName = "CustomerEmail",
                                PropertyType = "System.String",
                                OldValue = "alice@example.com",
                                NewValue = "alice@example.com"
                            },
                            new AuditEntryProperty
                            {
                                PropertyName = "Total",
                                PropertyType = "System.Decimal",
                                OldValue = "149.99",
                                NewValue = "149.99"
                            }
                        }
                    }
                }
            };

            // The pipeline enriches the transaction with the user/source/correlation
            // context, applies GDPR storage policies, then persists via IAuditStore.
            var pipeline = sp.GetRequiredService<AuditPipeline>();
            var result = await pipeline.ProcessAsync(transaction, cancellationToken);

            if (result.IsSuccess)
                _logger.LogInformation(
                    "Audit recorded for Order #{OrderId} — status changed to Shipped.",
                    42);
            else
                _logger.LogWarning("Audit pipeline returned failure.");
        }
    }
}