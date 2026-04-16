using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using RzR.DataVigil.Abstractions.Contracts;
using RzR.DataVigil.EFCore.Tests.Entities;

namespace RzR.DataVigil.EFCore.Tests.Data
{
    public class AuditableTestDbContext : DbContext, IAuditableContext
    {
        public AuditableTestDbContext(DbContextOptions<AuditableTestDbContext> options) : base(options)
        {
        }

        public DbSet<AuditableOrder> Orders { get; set; }

        public DbSet<SelectiveAuditProduct> Products { get; set; }

        public DbSet<FieldExclusionEntity> FieldExclusions { get; set; }

        public DbSet<NonAuditableLog> Logs { get; set; }

        public IEnumerable<Type> GetExcludedEntityTypes() => Enumerable.Empty<Type>();

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

            modelBuilder.Entity<FieldExclusionEntity>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();
                entity.Property(e => e.PublicNote).HasMaxLength(256);
                entity.Property(e => e.SecretNote).HasMaxLength(256);
                entity.Property(e => e.InternalCode).HasMaxLength(128);
                entity.Ignore(e => e.AllowedActions);
                entity.Ignore(e => e.ExcludedFieldNames);
            });

            modelBuilder.Entity<NonAuditableLog>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();
                entity.Property(e => e.Message).HasMaxLength(1024);
            });
        }
    }
}
