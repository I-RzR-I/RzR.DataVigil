using System;
using System.Collections.Generic;
using System.Linq;
using RzR.DataVigil.Abstractions.Enums;
using RzR.DataVigil.Abstractions.Models.Entries.Referees;

namespace RzR.DataVigil.EFCore.Tests.Helpers
{
    internal static class AuditReferenceTableCatalog
    {
        public static IEnumerable<ReferenceTable> All()
        {
            yield return new ReferenceTable("RefAuditActions", typeof(AuditAction), typeof(RefAuditAction));

            yield return new ReferenceTable("RefAuditUserSources", typeof(AuditUserSource),
                typeof(RefAuditUserSource));

            yield return new ReferenceTable("RefGdprFieldActions", typeof(GdprFieldAction),
                typeof(RefGdprFieldAction));

            yield return new ReferenceTable("RefGdprStorageStates", typeof(GdprStorageState),
                typeof(RefGdprStorageState));
        }

        public static List<EnumMember> EnumMembersOf(Type enumType)
        {
            return Enum.GetValues(enumType)
                .Cast<object>()
                .Select(v => new EnumMember(Enum.GetName(enumType, v), Convert.ToInt32(v)))
                .ToList();
        }
    }
}
