using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using MongoDB.EntityFrameworkCore.Extensions;
using RzR.DataVigil.Abstractions.Contracts;
using WebApiEfMongoDbNet8.Models;

namespace WebApiEfMongoDbNet8.Data
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

            modelBuilder.Entity<Post>(entity =>
            {
                entity.ToCollection("posts");
                entity.HasKey(p => p.Id);
                entity.Property(p => p.Title).IsRequired();
                entity.Property(p => p.Body).IsRequired();
                entity.Property(p => p.Author).IsRequired();
            });

            modelBuilder.Entity<Comment>(entity =>
            {
                entity.ToCollection("comments");
                entity.HasKey(c => c.Id);
                entity.Property(c => c.Author).IsRequired();
                entity.Property(c => c.Content).IsRequired();
            });
        }
    }
}
