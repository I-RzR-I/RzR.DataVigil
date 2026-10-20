using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RzR.DataVigil.Abstractions.Enums;
using RzR.DataVigil.EFCore.Helpers;
using RzR.Extensions.Domain.Collections;

namespace RzR.DataVigil.EFCore.Tests
{
    [TestClass]
    public class CompositeEntityIdFormatTests
    {
        private CompositeIdDbContext _db;

        [TestInitialize]
        public void Init()
        {
            var options = new DbContextOptionsBuilder<CompositeIdDbContext>()
                .UseInMemoryDatabase("CompositeId_" + Guid.NewGuid().ToString("N"))
                .Options;

            _db = new CompositeIdDbContext(options);
        }

        [TestCleanup]
        public void Cleanup()
        {
            _db?.Dispose();
        }

        [TestMethod]
        public void Build_Added_IntAndStringKey_EntityIdIsCommaJoined()
        {
            _db.Add(new IntStringKeyRow { TenantId = 1, Code = "abc", Name = "first" });
            _db.ChangeTracker.DetectChanges();

            var entry = _db.ChangeTracker.Entries<IntStringKeyRow>().Single();
            var result = ChangeTrackerEntryBuilder.Build(entry);

            Assert.AreEqual(AuditAction.Create, result.Action);
            Assert.AreEqual("1,abc", result.EntityId);
        }

        [TestMethod]
        public void Build_Modified_IntAndStringKey_EntityIdIsCommaJoined()
        {
            var row = new IntStringKeyRow { TenantId = 1, Code = "abc", Name = "first" };
            _db.Attach(row);
            row.Name = "renamed";
            _db.ChangeTracker.DetectChanges();

            var entry = _db.ChangeTracker.Entries<IntStringKeyRow>().Single();
            Assert.AreEqual(EntityState.Modified, entry.State);

            var result = ChangeTrackerEntryBuilder.Build(entry);

            Assert.AreEqual(AuditAction.Update, result.Action);
            Assert.AreEqual("1,abc", result.EntityId);
        }

        [TestMethod]
        public void Build_Deleted_IntAndStringKey_EntityIdIsCommaJoined()
        {
            var row = new IntStringKeyRow { TenantId = 1, Code = "abc", Name = "first" };
            _db.Attach(row);
            _db.Remove(row);

            var entry = _db.ChangeTracker.Entries<IntStringKeyRow>().Single();
            Assert.AreEqual(EntityState.Deleted, entry.State);

            var result = ChangeTrackerEntryBuilder.Build(entry);

            Assert.AreEqual(AuditAction.Delete, result.Action);
            Assert.AreEqual("1,abc", result.EntityId);
        }

        [TestMethod]
        public void Build_Added_KeyPartContainsComma_IsNotEscaped()
        {
            _db.Add(new StringStringKeyRow { Region = "a,b", Code = "c", Name = "first" });
            _db.ChangeTracker.DetectChanges();

            var entry = _db.ChangeTracker.Entries<StringStringKeyRow>().Single();
            var result = ChangeTrackerEntryBuilder.Build(entry);

            Assert.AreEqual("a,b,c", result.EntityId);
        }

        [TestMethod]
        public void Build_Added_KeyPartIsWhitespace_IsKeptVerbatim()
        {
            _db.Add(new StringStringKeyRow { Region = " x ", Code = "\t", Name = "first" });
            _db.ChangeTracker.DetectChanges();

            var entry = _db.ChangeTracker.Entries<StringStringKeyRow>().Single();
            var result = ChangeTrackerEntryBuilder.Build(entry);

            Assert.AreEqual(" x ,\t", result.EntityId);
        }

        [TestMethod]
        public void ListToString_NullSubstitutePart_YieldsNullLiteral()
        {
            var parts = new List<string> { "1", "null" };

            var joined = parts.ListToString(",");

            Assert.AreEqual("1,null", joined);
        }

        [TestMethod]
        public void ListToString_NullElement_YieldsEmptySegment()
        {
            var parts = new List<string> { "1", null };

            var joined = parts.ListToString(",");

            Assert.AreEqual("1,", joined);
        }

        [TestMethod]
        public void Build_Added_ConverterYieldsNullForKeyPart_UsesNullLiteral()
        {
            _db.Add(new ConvertedKeyRow { TenantId = 1, Code = "none", Name = "first" });
            _db.ChangeTracker.DetectChanges();

            var entry = _db.ChangeTracker.Entries<ConvertedKeyRow>().Single();
            var result = ChangeTrackerEntryBuilder.Build(entry);

            Assert.AreEqual("1,null", result.EntityId);
        }

        private sealed class IntStringKeyRow
        {
            public int TenantId { get; set; }

            public string Code { get; set; }

            public string Name { get; set; }
        }

        private sealed class StringStringKeyRow
        {
            public string Region { get; set; }

            public string Code { get; set; }

            public string Name { get; set; }
        }

        private sealed class ConvertedKeyRow
        {
            public int TenantId { get; set; }

            public string Code { get; set; }

            public string Name { get; set; }
        }

        private sealed class CompositeIdDbContext : DbContext
        {
            public CompositeIdDbContext(DbContextOptions<CompositeIdDbContext> options)
                : base(options)
            {
            }

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                modelBuilder.Entity<IntStringKeyRow>()
                    .HasKey(x => new { x.TenantId, x.Code });

                modelBuilder.Entity<StringStringKeyRow>()
                    .HasKey(x => new { x.Region, x.Code });

                modelBuilder.Entity<ConvertedKeyRow>(builder =>
                {
                    builder.HasKey(x => new { x.TenantId, x.Code });
                    builder.Property(x => x.Code)
                        .HasConversion(v => v == "none" ? null : v, v => v);
                });
            }
        }
    }
}
