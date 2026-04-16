using RzR.DataVigil.Abstractions.Contracts;

namespace SampleWorkerService.Models
{
    /// <summary>
    ///     Sample entity implementing IAuditable to opt into audit trail capture.
    /// </summary>
    public class Order : IAuditable
    {
        public int Id { get; set; }

        public string CustomerName { get; set; }

        public string CustomerEmail { get; set; }

        public string CustomerPhone { get; set; }

        public decimal Total { get; set; }

        public string Status { get; set; }
    }
}
