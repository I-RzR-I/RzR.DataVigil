using RzR.DataVigil.Abstractions.Contracts;

namespace RzR.DataVigil.EFCore.Tests.Entities
{
    public class IdentityKeyedOrder : IAuditable
    {
        public int Id { get; set; }

        public string CustomerName { get; set; }

        public decimal Total { get; set; }
    }
}
