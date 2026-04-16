using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using RzR.DataVigil.Abstractions.Contracts;
using RzR.DataVigil.EFCore.Tests.Entities;

namespace RzR.DataVigil.EFCore.Tests.Data
{
    /// <summary>
    ///     DbContext that excludes AuditableOrder via context-level GetExcludedEntityTypes().
    /// </summary>
    public class ContextExcludingOrderDbContext : DbContext, IAuditableContext
    {
        public ContextExcludingOrderDbContext(DbContextOptions<ContextExcludingOrderDbContext> options)
            : base(options)
        {
        }

        public DbSet<AuditableOrder> Orders { get; set; }

        public DbSet<SelectiveAuditProduct> Products { get; set; }

        public IEnumerable<Type> GetExcludedEntityTypes()
        {
            return new[] { typeof(AuditableOrder) };
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<AuditableOrder>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();
                entity.Property(e => e.CustomerName).HasMaxLength(256);
                entity.Property(e => e.Total).HasColumnType("decimal(18,2)");
            });

            modelBuilder.Entity<SelectiveAuditProduct>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();
                entity.Property(e => e.Name).HasMaxLength(256);
                entity.Property(e => e.Price).HasColumnType("decimal(18,2)");
                entity.Ignore(e => e.AllowedActions);
            });
        }
    }
}
