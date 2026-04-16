using System;
using System.Collections.Generic;
using RzR.DataVigil.Abstractions.Enums;
using RzR.DataVigil.Abstractions.Models.Entries;
using RzR.DataVigil.Core.Gdpr;

namespace RzR.DataVigil.Storage.EfSqlServer.Tests.Helpers
{
    internal static class AuditTestDataBuilder
    {
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

            // Auto-assign TransactionId to entries
            foreach (var entry in txn.Entries)
            {
                entry.TransactionId = txn.Id;
            }

            return txn;
        }

        internal static AuditEntry BuildEntry(
            string entityName = "Order",
            string entityId = "42",
            AuditAction action = AuditAction.Create,
            ICollection<AuditEntryProperty> properties = null)
        {
            return new AuditEntry
            {
                Id = Guid.NewGuid(),
                Action = action,
                EntityName = entityName,
                EntityId = entityId,
                EntityTypeName = "TestApp.Domain." + entityName,
                Properties = properties ?? new List<AuditEntryProperty>()
            };
        }

        internal static AuditEntryProperty BuildProperty(string name, string oldValue, string newValue) =>
            new AuditEntryProperty
            {
                PropertyName = name,
                PropertyType = "System.String",
                OldValue = oldValue,
                NewValue = newValue
            };

        internal static GdprPolicyRegistry CreateRegistryWithPolicy(string entityName, EntityGdprPolicy policy)
        {
            var registry = new GdprPolicyRegistry();
            var field = typeof(GdprPolicyRegistry).GetField("_policiesByName",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var dict = (IDictionary<string, EntityGdprPolicy>)field.GetValue(registry);
            dict[entityName] = policy;

            return registry;
        }
    }
}
