using System;
using System.ComponentModel.DataAnnotations;

namespace RzR.DataVigil.EFCore.Tests.Entities
{
    public class NonAuditableLog
    {
        [Key]
        public Guid Id { get; set; }

        public string Message { get; set; }
    }
}
