using RzR.DataVigil.Abstractions.Contracts;

namespace RzR.DataVigil.Core.Tests.Models
{
    internal class Order : IAuditable
    {
        public int Id { get; set; }

        public string CustomerEmail { get; set; }

        public string CustomerPhone { get; set; }

        public decimal TotalAmount { get; set; }

        public string ShippingAddress { get; set; }
    }
}
