using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using RzR.DataVigil.Abstractions.Enums;
using RzR.DataVigil.Abstractions.Models.Entries;

namespace RzR.DataVigil.AspNetCore.Tests.Stubs
{
    internal static class HttpEnricherTestData
    {
        internal static AuditTransaction BuildTransaction() => new AuditTransaction
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow,
            Entries = new List<AuditEntry>
            {
                new AuditEntry
                {
                    Id = Guid.NewGuid(),
                    EntityName = "Order",
                    EntityId = "1",
                    Action = AuditAction.Update,
                    Properties = new List<AuditEntryProperty>
                    {
                        new AuditEntryProperty
                        {
                            PropertyName = "Name",
                            PropertyType = "System.String",
                            OldValue = "Old",
                            NewValue = "New"
                        }
                    }
                }
            }
        };

        internal static DefaultHttpContext ContextWithMethod(string method)
        {
            var context = new DefaultHttpContext();
            context.Request.Method = method;

            return context;
        }
    }
}
