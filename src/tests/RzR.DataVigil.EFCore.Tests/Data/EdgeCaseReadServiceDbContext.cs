using System.Collections.Generic;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RzR.DataVigil.EFCore.Tests.Entities;

namespace RzR.DataVigil.EFCore.Tests.Data
{
    public class EdgeCaseReadServiceDbContext : DbContext
    {
        public const string ShadowPropertyName = "InternalNote";

        public EdgeCaseReadServiceDbContext(DbContextOptions<EdgeCaseReadServiceDbContext> options) : base(options)
        {
        }

        public DbSet<EdgeCaseReadOrder> Orders { get; set; }

        public static string Serialize(Dictionary<string, string> value)
        {
            return JsonSerializer.Serialize(value, (JsonSerializerOptions)null);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<EdgeCaseReadOrder>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.CustomerName).HasMaxLength(256);

                entity.Property(e => e.Labels)
                    .HasConversion(
                        v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null),
                        v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, (JsonSerializerOptions)null));

                entity.Property<string>(ShadowPropertyName);
            });
        }
    }
}
