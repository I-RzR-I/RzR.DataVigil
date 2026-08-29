#region U S A G E S

using System;
using System.Collections.Generic;
using System.Reflection;
using RzR.DataVigil.Abstractions.Enums;
using RzR.DataVigil.Abstractions.Models.Entries;
using RzR.DataVigil.Core.Gdpr;

#endregion

namespace RzR.DataVigil.TestSupport
{
    public static class AuditTestDataBuilder
    {
        public static AuditTransaction BuildTransaction(params AuditEntry[] entries) => new AuditTransaction
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow,
            Entries = new List<AuditEntry>(entries)
        };

        public static AuditTransaction BuildTransaction(
            string userId = "user1",
            string userName = "User One",
            string ipAddress = "127.0.0.1",
            DateTimeOffset? timestamp = null,
            string source = "Tests",
            List<AuditEntry> entries = null,
            Guid? id = null,
            string correlationId = null,
            GdprStorageState gdprState = GdprStorageState.Original)
        {
            var txn = new AuditTransaction
            {
                Id = id ?? Guid.NewGuid(),
                Timestamp = timestamp ?? DateTimeOffset.UtcNow,
                UserId = userId,
                UserName = userName,
                IpAddress = ipAddress,
                CorrelationId = correlationId,
                GdprState = gdprState,
                Source = source,
                Entries = entries ?? new List<AuditEntry>()
            };

            foreach (var entry in txn.Entries)
                entry.TransactionId = txn.Id;

            return txn;
        }

        public static AuditEntry BuildEntry(
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

        public static AuditEntry BuildPipelineEntry(string entityName = "Order")
            => BuildEntryWithProperties(entityName, BuildProperty("Name", "Old", "New"));

        public static AuditEntry BuildEntryWithProperties(string entityName, params AuditEntryProperty[] props)
        {
            return new AuditEntry
            {
                Id = Guid.NewGuid(),
                EntityName = entityName,
                EntityId = "1",
                Action = AuditAction.Update,
                Properties = new List<AuditEntryProperty>(props)
            };
        }

        public static AuditEntryProperty BuildProperty(string name, string oldValue, string newValue) =>
            new AuditEntryProperty
            {
                PropertyName = name,
                PropertyType = "System.String",
                OldValue = oldValue,
                NewValue = newValue
            };

        public static AuditEntryProperty Prop(string name, string oldValue, string newValue)
            => BuildProperty(name, oldValue, newValue);

        public static GdprPolicyRegistry CreateRegistryWithPolicy(string entityName, EntityGdprPolicy policy)
        {
            var registry = new GdprPolicyRegistry();
            var field = typeof(GdprPolicyRegistry).GetField("_policiesByName",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var dict = (IDictionary<string, EntityGdprPolicy>)field.GetValue(registry);
            dict[entityName] = policy;

            return registry;
        }
    }
}
