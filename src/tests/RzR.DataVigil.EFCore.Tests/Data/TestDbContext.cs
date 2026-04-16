using Microsoft.EntityFrameworkCore;
using RzR.DataVigil.EFCore.Tests.Entities;

namespace RzR.DataVigil.EFCore.Tests.Data
{
    public class TestDbContext : DbContext
    {
        public TestDbContext(DbContextOptions<TestDbContext> options) : base(options)
        {
        }

        public DbSet<TestOrder> Orders { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<TestOrder>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();
                entity.Property(e => e.CustomerName).HasMaxLength(256);
                entity.Property(e => e.Total).HasColumnType("decimal(18,2)");
            });
        }
    }
}
