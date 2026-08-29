using Microsoft.EntityFrameworkCore;

namespace RzR.DataVigil.EFCore.Tests.Data
{
    public class ReferenceDataTestDbContext : AuditDbContextBase
    {
        public ReferenceDataTestDbContext(DbContextOptions<ReferenceDataTestDbContext> options)
            : base(options)
        {
        }
    }
}
