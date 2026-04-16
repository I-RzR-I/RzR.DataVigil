using RzR.DataVigil.Abstractions.Contracts;

namespace RzR.DataVigil.Core.Tests.Models
{
    internal class CustomerEntity : IAuditable
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public string Email { get; set; }

        public string Ssn { get; set; }

        public string Phone { get; set; }
    }
}
