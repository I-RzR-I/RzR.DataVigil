using System;
using System.Collections.Generic;
using RzR.DataVigil.Abstractions.Enums;
using RzR.DataVigil.Abstractions.Models.Entries;
using RzR.DataVigil.Abstractions.Models.Gdpr;
using RzR.DataVigil.Core.Gdpr;

namespace RzR.DataVigil.Storage.File.Tests.Helpers
{
    internal static class AuditTestDataBuilder
    {
        internal static GdprPolicyRegistry CreateRegistryWithPolicy(string entityName, EntityGdprPolicy policy)
        {
            var registry = new GdprPolicyRegistry();
            var field = typeof(GdprPolicyRegistry).GetField("_policiesByName",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var dict = (IDictionary<string, EntityGdprPolicy>)field.GetValue(registry);
            dict[entityName] = policy;
            return registry;
        }

        internal static AuditTransaction BuildTransaction(
            string userId = "user1",
            string userName = "User One",
            string ipAddress = "127.0.0.1",
            DateTimeOffset? timestamp = null,
            string source = "Tests",
            List<AuditEntry> entries = null)
        {
            var txn = new AuditTransaction
            {
                Id = Guid.NewGuid(),
                Timestamp = timestamp ?? DateTimeOffset.UtcNow,
                UserId = userId,
                UserName = userName,
                IpAddress = ipAddress,
                GdprState = GdprStorageState.Original,
                Source = source,
                Entries = entries ?? new List<AuditEntry>()
            };

            foreach (var entry in txn.Entries)
                entry.TransactionId = txn.Id;

            return txn;
        }

        internal static AuditEntry BuildEntry(
            string entityName = "Order",
            string entityId = "42",
            AuditAction action = AuditAction.Create)
        {
            return new AuditEntry
            {
                Id = Guid.NewGuid(),
                Action = action,
                EntityName = entityName,
                EntityId = entityId,
                EntityTypeName = "TestApp.Domain." + entityName
            };
        }
    }
}
