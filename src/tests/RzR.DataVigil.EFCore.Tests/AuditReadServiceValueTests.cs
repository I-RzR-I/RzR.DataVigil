using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RzR.DataVigil.Abstractions.Enums;
using RzR.DataVigil.Abstractions.Models.Entries;
using RzR.DataVigil.Core.Gdpr;
using RzR.DataVigil.EFCore;
using RzR.DataVigil.Core.Options;
using RzR.DataVigil.Core.Pipeline;
using RzR.DataVigil.EFCore.Tests.Data;
using RzR.DataVigil.EFCore.Tests.Entities;
using RzR.DataVigil.EFCore.Tests.Stubs;

namespace RzR.DataVigil.EFCore.Tests
{
    [TestClass]
    public class AuditReadServiceValueTests
    {
        private EdgeCaseReadServiceDbContext _db;
        private StubAuditStore _store;
        private AuditReadService _readService;

        [TestInitialize]
        public void Init()
        {
            var options = new DbContextOptionsBuilder<EdgeCaseReadServiceDbContext>()
                .UseInMemoryDatabase("AuditReadServiceValue_" + Guid.NewGuid().ToString("N"))
                .Options;

            _db = new EdgeCaseReadServiceDbContext(options);

            _store = new StubAuditStore();

            var auditOptions = new AuditTrailOptions();
            auditOptions.EfCore.Intercept<EdgeCaseReadServiceDbContext>();
            auditOptions.EfCore.IncludeReads();
            auditOptions.EfCore.IncludeReadProperties();
            ForceIncludeReadPropertiesValue(auditOptions.EfCore);

            var pipeline = new AuditPipeline(
                new StubUserResolver(),
                new StubSourceResolver(),
                new StubCorrelationProvider(),
                new GdprProcessor(new GdprPolicyRegistry()),
                _store);

            var loggerFactory = LoggerFactory.Create(_ => { });

            _readService = new AuditReadService(auditOptions, pipeline, loggerFactory.CreateLogger<AuditReadService>());
        }

        [TestCleanup]
        public void Cleanup()
        {
            _db?.Dispose();
        }

        [TestMethod]
        public async System.Threading.Tasks.Task LogReadAsync_ComplexValueProperty_RecordsJson()
        {
            var order = NewOrder();
            order.Tags = new List<string> { "red", "green" };

            _db.Orders.Add(order);
            _db.SaveChanges();
            _db.ChangeTracker.Clear();

            var loaded = _db.Orders.AsNoTracking().Single(o => o.Id == order.Id);

            await _readService.LogReadAsync(_db, loaded);

            var property = SingleReadProperty(nameof(EdgeCaseReadOrder.Tags));
            Assert.AreEqual("[\"red\",\"green\"]", property.NewValue);
            Assert.IsNull(property.OldValue, "Read entries never carry an OldValue.");
        }

        [TestMethod]
        public async System.Threading.Tasks.Task LogReadAsync_ConverterMappedProperty_RecordsProviderValue()
        {
            var order = NewOrder();
            order.Labels = new Dictionary<string, string> { { "en", "Name" }, { "ro", "Nume" } };

            _db.Orders.Add(order);
            _db.SaveChanges();
            _db.ChangeTracker.Clear();

            var loaded = _db.Orders.AsNoTracking().Single(o => o.Id == order.Id);

            await _readService.LogReadAsync(_db, loaded);

            var property = SingleReadProperty(nameof(EdgeCaseReadOrder.Labels));
            Assert.AreEqual(EdgeCaseReadServiceDbContext.Serialize(loaded.Labels), property.NewValue);
        }

        [TestMethod]
        public async System.Threading.Tasks.Task LogReadAsync_ShadowProperty_RecordsNullBecauseNoClrPropertyBacksIt()
        {
            var order = NewOrder();

            _db.Orders.Add(order);
            _db.Entry(order).Property(EdgeCaseReadServiceDbContext.ShadowPropertyName).CurrentValue = "internal-note-1";
            _db.SaveChanges();
            _db.ChangeTracker.Clear();

            var loaded = _db.Orders.AsNoTracking().Single(o => o.Id == order.Id);

            await _readService.LogReadAsync(_db, loaded);

            var property = SingleReadProperty(EdgeCaseReadServiceDbContext.ShadowPropertyName);

            Assert.IsNull(property.NewValue);
        }

        [TestMethod]
        public async System.Threading.Tasks.Task LogReadAsync_PlainProperty_IsStillRecordedAlongsideTheEdgeCaseOnes()
        {
            var order = NewOrder();

            _db.Orders.Add(order);
            _db.SaveChanges();
            _db.ChangeTracker.Clear();

            var loaded = _db.Orders.AsNoTracking().Single(o => o.Id == order.Id);

            await _readService.LogReadAsync(_db, loaded);

            var property = SingleReadProperty(nameof(EdgeCaseReadOrder.CustomerName));
            Assert.AreEqual("Alice", property.NewValue);
        }

        private AuditEntryProperty SingleReadProperty(string propertyName)
        {
            Assert.AreEqual(1, _store.SavedTransactions.Count, "Exactly one Read transaction is expected per call.");

            var entry = _store.SavedTransactions[0].Entries.Single();
            Assert.AreEqual(RzR.DataVigil.Abstractions.Enums.AuditAction.Read, entry.Action);

            return entry.Properties.Single(p => p.PropertyName == propertyName);
        }

        private static void ForceIncludeReadPropertiesValue(EfCoreAuditOptions efCoreOptions)
        {
            var property = typeof(EfCoreAuditOptions)
                .GetProperty(nameof(EfCoreAuditOptions.IncludeReadPropertiesValueEnabled));
            var setter = property?.GetSetMethod(nonPublic: true);

            Assert.IsNotNull(setter,
                "EfCoreAuditOptions.IncludeReadPropertiesValueEnabled must have a settable backing accessor " +
                "for this reflection-based test harness to work; if this assertion ever fails, the public " +
                "API gap it works around (no fluent method sets this flag to true) should be raised with the " +
                "architect instead of papered over here.");

            setter.Invoke(efCoreOptions, new object[] { true });
        }

        private static EdgeCaseReadOrder NewOrder()
        {
            return new EdgeCaseReadOrder
            {
                Id = Guid.NewGuid(),
                CustomerName = "Alice"
            };
        }
    }
}
