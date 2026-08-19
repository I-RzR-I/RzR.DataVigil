namespace RzR.DataVigil.EFCore.Tests.Entities
{
    public class AuditOutboxRecord
    {
        public int Id { get; set; }

        public string Topic { get; set; }

        public string Payload { get; set; }
    }
}
