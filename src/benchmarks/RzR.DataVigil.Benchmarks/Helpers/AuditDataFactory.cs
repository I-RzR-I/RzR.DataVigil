// ***********************************************************************
//  Assembly          : RzR.DataVigil.RzR.DataVigil.Benchmarks
//  Author            : RzR
//  Created           : 20-04-2026 08:04
// 
//  Last Modified By : RzR
//  Last Modified On : 20-05-2026 23:08
//  ***********************************************************************
//  <copyright file="AuditDataFactory.cs" company="RzR SOFT & TECH">
//      Copyright (c) RzR. All rights reserved.
//  </copyright>
//  <contact>
//      https://iamrzr.dev/contact
//  </contact>
//  <summary></summary>
//  ***********************************************************************

#region U S I N G

using System;
using System.Collections.Generic;
using RzR.DataVigil.Abstractions.Enums;
using RzR.DataVigil.Abstractions.Models.Entries;

#endregion

namespace RzR.DataVigil.Benchmarks.Helpers
{
    internal static class AuditDataFactory
    {
        internal static AuditEntry CreateEntry(string entityName, int propertyCount)
        {
            var entry = new AuditEntry
            {
                Id = Guid.NewGuid(),
                TransactionId = Guid.NewGuid(),
                Action = AuditAction.Update,
                EntityName = entityName,
                EntityTypeName = $"Benchmark.Models.{entityName}",
                EntityId = Guid.NewGuid().ToString()
            };

            for (var i = 0; i < propertyCount; i++)
                entry.Properties.Add(new AuditEntryProperty
                {
                    PropertyName = $"Property{i}",
                    PropertyType = "System.String",
                    OldValue = $"OldValue_{i}_{Guid.NewGuid():N}",
                    NewValue = $"NewValue_{i}_{Guid.NewGuid():N}"
                });

            return entry;
        }

        internal static AuditTransaction CreateTransaction(int entryCount, int propertiesPerEntry)
        {
            var txn = new AuditTransaction
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTimeOffset.UtcNow,
                UserId = "bench-user-001",
                UserName = "Benchmark User",
                IpAddress = "192.168.1.100",
                CorrelationId = Guid.NewGuid().ToString("N"),
                TraceId = Guid.NewGuid().ToString("N"),
                Source = "BenchmarkRunner",
                GdprState = GdprStorageState.Original
            };

            for (var i = 0; i < entryCount; i++)
            {
                var entry = CreateEntry($"Entity{i}", propertiesPerEntry);
                entry.TransactionId = txn.Id;
                txn.Entries.Add(entry);
            }

            return txn;
        }

        internal static AuditTransaction CreateOrderTransaction()
        {
            var txn = new AuditTransaction
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTimeOffset.UtcNow,
                UserId = "user-42",
                UserName = "Jane Doe",
                IpAddress = "10.0.0.5",
                CorrelationId = Guid.NewGuid().ToString("N"),
                TraceId = Guid.NewGuid().ToString("N"),
                Source = "WebApi",
                GdprState = GdprStorageState.Original
            };

            var entry = new AuditEntry
            {
                Id = Guid.NewGuid(),
                TransactionId = txn.Id,
                Action = AuditAction.Update,
                EntityName = "Order",
                EntityTypeName = "SampleWorkerService.Models.Order",
                EntityId = "789"
            };

            entry.Properties.Add(new AuditEntryProperty
            {
                PropertyName = "CustomerName",
                PropertyType = "System.String",
                OldValue = "Alice Smith",
                NewValue = "Alice Johnson"
            });
            entry.Properties.Add(new AuditEntryProperty
            {
                PropertyName = "CustomerEmail",
                PropertyType = "System.String",
                OldValue = "alice@example.com",
                NewValue = "alice.j@example.com"
            });
            entry.Properties.Add(new AuditEntryProperty
            {
                PropertyName = "CustomerPhone",
                PropertyType = "System.String",
                OldValue = "+1-555-0100",
                NewValue = "+1-555-0200"
            });
            entry.Properties.Add(new AuditEntryProperty
                {
                    PropertyName = "Total",
                    PropertyType = "System.Decimal",
                    OldValue = "99.99",
                    NewValue = "109.99"
                }
            );
            entry.Properties.Add(new AuditEntryProperty
            {
                PropertyName = "Status",
                PropertyType = "System.String",
                OldValue = "Pending",
                NewValue = "Shipped"
            });

            txn.Entries.Add(entry);

            return txn;
        }

        internal static List<AuditTransaction> CreateTransactionBatch(int count, int entriesPerTxn,
            int propertiesPerEntry)
        {
            var list = new List<AuditTransaction>(count);
            for (var i = 0; i < count; i++)
            {
                var txn = CreateTransaction(entriesPerTxn, propertiesPerEntry);
                txn.UserId = $"user-{i % 50}";
                txn.UserName = $"User {i % 50}";
                txn.Timestamp = DateTimeOffset.UtcNow.AddMinutes(-i);
                list.Add(txn);
            }

            return list;
        }
    }
}