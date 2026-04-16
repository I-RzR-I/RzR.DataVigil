using System;
using System.ComponentModel.DataAnnotations;

namespace RzR.DataVigil.EFCore.Tests.Entities
{
    /// <summary>
    ///     Entity that does NOT implement IAuditable — should never be audited.
    /// </summary>
    public class NonAuditableLog
    {
        [Key]
        public Guid Id { get; set; }

        public string Message { get; set; }
    }
}
