using System;
using System.Collections.Generic;
using RzR.DataVigil.Abstractions.Enums;
using RzR.DataVigil.Abstractions.Models.Entries;

namespace RzR.DataVigil.Core.Tests.Helpers
{
    internal static class AuditTestDataBuilder
    {
        internal static AuditEntry BuildEntryWithProperties(string entityName, params AuditEntryProperty[] props)
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

        internal static AuditEntryProperty Prop(string name, string oldVal, string newVal) =>
            new AuditEntryProperty { PropertyName = name, PropertyType = "System.String", OldValue = oldVal, NewValue = newVal };

        internal static AuditTransaction BuildTransaction(params AuditEntry[] entries) => new AuditTransaction
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow,
            Entries = new List<AuditEntry>(entries)
        };

        internal static AuditEntry BuildEntry(string entityName = "Order") => new AuditEntry
        {
            Id = Guid.NewGuid(),
            EntityName = entityName,
            EntityId = "1",
            Action = AuditAction.Update,
            Properties = new List<AuditEntryProperty>
            {
                new AuditEntryProperty { PropertyName = "Name", PropertyType = "System.String", OldValue = "Old", NewValue = "New" }
            }
        };
    }
}
