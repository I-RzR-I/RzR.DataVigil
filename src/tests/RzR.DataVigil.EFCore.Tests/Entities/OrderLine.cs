using RzR.DataVigil.Abstractions.Contracts;

namespace RzR.DataVigil.EFCore.Tests.Entities
{
    public class OrderLine : IAuditable
    {
        public int Id { get; set; }

        public int OrderId { get; set; }

        public IdentityKeyedOrder Order { get; set; }

        public string Note { get; set; }
    }
}
