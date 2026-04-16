using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using RzR.DataVigil.Abstractions.Contracts;
using WebApiEfPostgreSqlNet7.Models;

namespace WebApiEfPostgreSqlNet7.Data
{
    public class BlogDbContext : DbContext, IAuditableContext
    {
        public BlogDbContext(DbContextOptions<BlogDbContext> options) : base(options)
        {
        }

        public DbSet<Post> Posts { get; set; }
        public DbSet<Comment> Comments { get; set; }

        public IEnumerable<Type> GetExcludedEntityTypes() => Enumerable.Empty<Type>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.HasDefaultSchema("blog");

            modelBuilder.Entity<Post>(entity =>
            {
                entity.HasKey(p => p.Id);
                entity.Property(p => p.Id).ValueGeneratedOnAdd();
                entity.Property(p => p.Title).IsRequired().HasMaxLength(256);
                entity.Property(p => p.Body).IsRequired();
                entity.Property(p => p.Author).IsRequired().HasMaxLength(128);
                entity.Property(p => p.CreatedAt).IsRequired();
                entity.Property(p => p.UpdatedAt).IsRequired();
            });

            modelBuilder.Entity<Comment>(entity =>
            {
                entity.HasKey(c => c.Id);
                entity.Property(c => c.Id).ValueGeneratedOnAdd();
                entity.Property(c => c.Author).IsRequired().HasMaxLength(128);
                entity.Property(c => c.Content).IsRequired();
                entity.Property(c => c.CreatedAt).IsRequired();

                entity.HasOne(c => c.Post)
                    .WithMany(p => p.Comments)
                    .HasForeignKey(c => c.PostId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
