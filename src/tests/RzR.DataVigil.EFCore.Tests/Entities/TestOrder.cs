using System;
using System.ComponentModel.DataAnnotations;

namespace RzR.DataVigil.EFCore.Tests.Entities
{
    public class TestOrder
    {
        [Key]
        public Guid Id { get; set; }

        public string CustomerName { get; set; }

        public decimal Total { get; set; }

        public DateTime? ShippedAt { get; set; }

        public int Quantity { get; set; }
    }
}
