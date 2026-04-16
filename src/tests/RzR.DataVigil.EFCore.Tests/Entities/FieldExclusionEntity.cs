using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using RzR.DataVigil.Abstractions.Contracts;
using RzR.DataVigil.Abstractions.Enums;

namespace RzR.DataVigil.EFCore.Tests.Entities
{
    /// <summary>
    ///     Entity that excludes specific fields from audit via IAuditableEntity.GetExcludedFields().
    /// </summary>
    public class FieldExclusionEntity : IAuditableEntity
    {
        [Key]
        public Guid Id { get; set; }

        public string PublicNote { get; set; }

        public string SecretNote { get; set; }

        public string InternalCode { get; set; }

        public HashSet<AuditAction> AllowedActions { get; set; }
            = new HashSet<AuditAction> { AuditAction.Create, AuditAction.Update, AuditAction.Delete };

        public HashSet<string> ExcludedFieldNames { get; set; } = new HashSet<string>();

        public bool ShouldAudit(AuditAction action) => AllowedActions.Contains(action);

        public IEnumerable<string> GetExcludedFields() => ExcludedFieldNames;
    }
}
