using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using RzR.DataVigil.Abstractions.Contracts;
using RzR.DataVigil.EFCore.Tests.Entities;

namespace RzR.DataVigil.EFCore.Tests.Data
{
    public class SqliteTestDbContext : DbContext, IAuditableContext
    {
        public SqliteTestDbContext(DbContextOptions<SqliteTestDbContext> options) : base(options)
        {
        }

        public DbSet<IdentityKeyedOrder> Orders { get; set; }

        public DbSet<OrderLine> OrderLines { get; set; }

        public IEnumerable<Type> GetExcludedEntityTypes() => Enumerable.Empty<Type>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<IdentityKeyedOrder>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();
                entity.Property(e => e.CustomerName).HasMaxLength(256).IsRequired();
                entity.Property(e => e.Total).HasColumnType("decimal(18,2)");
                entity.HasIndex(e => e.CustomerName).IsUnique();
            });

            modelBuilder.Entity<OrderLine>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();
                entity.Property(e => e.Note).HasMaxLength(256);
                entity.HasOne(e => e.Order)
                    .WithMany()
                    .HasForeignKey(e => e.OrderId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
