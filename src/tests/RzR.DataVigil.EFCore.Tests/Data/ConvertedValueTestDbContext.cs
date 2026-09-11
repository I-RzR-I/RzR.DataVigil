using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using RzR.DataVigil.EFCore.Tests.Entities;

namespace RzR.DataVigil.EFCore.Tests.Data
{
    public class ConvertedValueTestDbContext : DbContext
    {
        private static long _nonDeterministicCallCounter;

        private const string NameI18nPrefix = "CONV|";

        public ConvertedValueTestDbContext(DbContextOptions<ConvertedValueTestDbContext> options) : base(options)
        {
        }

        public DbSet<ConvertedValueOrder> Orders { get; set; }

        public DbSet<ThrowingConverterOrder> ThrowingOrders { get; set; }

        public DbSet<ConvertedKeyOrder> KeyOrders { get; set; }

        public static string Serialize(Dictionary<string, string> value)
        {
            if (value == null)
                return NameI18nPrefix;

            var pairs = value.Keys.OrderBy(k => k, System.StringComparer.Ordinal).Select(k => k + "=" + value[k]);

            return NameI18nPrefix + string.Join("|", pairs);
        }

        private static Dictionary<string, string> Deserialize(string value)
        {
            var result = new Dictionary<string, string>();
            var body = value.Substring(NameI18nPrefix.Length);
            if (body.Length == 0)
                return result;

            foreach (var pair in body.Split('|'))
            {
                var separatorIndex = pair.IndexOf('=');
                result[pair.Substring(0, separatorIndex)] = pair.Substring(separatorIndex + 1);
            }

            return result;
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<ConvertedValueOrder>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.CustomerName).HasMaxLength(256);

                var i18n = entity.Property(e => e.NameI18n)
                    .HasConversion(v => Serialize(v), v => Deserialize(v));

                i18n.Metadata.SetValueComparer(new ValueComparer<Dictionary<string, string>>(
                    (l, r) => Serialize(l) == Serialize(r),
                    v => Serialize(v).GetHashCode(),
                    v => new Dictionary<string, string>(v)));

                entity.Property(e => e.SecretId)
                    .HasConversion(v => v.ToByteArray(), v => new System.Guid(v));

                entity.Property(e => e.NonDeterministicTag)
                    .HasConversion(
                        v => v + "#" + Interlocked.Increment(ref _nonDeterministicCallCounter),
                        v => v.Substring(0, v.LastIndexOf('#')));
            });

            modelBuilder.Entity<ThrowingConverterOrder>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Payload)
                    .HasConversion(v => ThrowingConverterOrder.FailingConvert(v), v => v);
            });

            modelBuilder.Entity<ConvertedKeyOrder>(entity =>
            {
                entity.Property(e => e.Id)
                    .HasConversion(v => v.ToByteArray(), v => new System.Guid(v));
                entity.HasKey(e => e.Id);
            });
        }
    }
}
