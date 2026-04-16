using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using RzR.DataVigil.Abstractions.Contracts;
using RzR.DataVigil.Abstractions.Enums;

namespace RzR.DataVigil.EFCore.Tests.Entities
{
    /// <summary>
    ///     Entity that implements IAuditableEntity with configurable ShouldAudit behaviour.
    /// </summary>
    public class SelectiveAuditProduct : IAuditableEntity
    {
        [Key]
        public Guid Id { get; set; }

        public string Name { get; set; }

        public decimal Price { get; set; }

        /// <summary>
        ///     Actions that this instance will allow to be audited.
        ///     When empty, ShouldAudit returns false for every action.
        /// </summary>
        public HashSet<AuditAction> AllowedActions { get; set; } = new HashSet<AuditAction>();

        public bool ShouldAudit(AuditAction action) => AllowedActions.Contains(action);

        public IEnumerable<string> GetExcludedFields() => Enumerable.Empty<string>();
    }
}
