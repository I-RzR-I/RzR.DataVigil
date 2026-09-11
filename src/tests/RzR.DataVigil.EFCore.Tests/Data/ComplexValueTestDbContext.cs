using Microsoft.EntityFrameworkCore;
using RzR.DataVigil.EFCore.Tests.Entities;

namespace RzR.DataVigil.EFCore.Tests.Data
{
    public class ComplexValueTestDbContext : DbContext
    {
        public ComplexValueTestDbContext(DbContextOptions<ComplexValueTestDbContext> options) : base(options)
        {
        }

        public DbSet<ComplexValueOrder> Orders { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<ComplexValueOrder>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.CustomerName).HasMaxLength(256);
                entity.Property(e => e.Status).HasConversion<int>();
            });
        }
    }
}
