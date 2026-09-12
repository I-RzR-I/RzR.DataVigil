using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RzR.DataVigil.Abstractions.Enums;
using RzR.DataVigil.EFCore.Helpers;
using RzR.DataVigil.EFCore.Tests.Data;
using RzR.DataVigil.EFCore.Tests.Entities;

namespace RzR.DataVigil.EFCore.Tests
{
    [TestClass]
    public class ChangeTrackerEntryBuilderConverterTests
    {
        private ConvertedValueTestDbContext _db;

        [TestInitialize]
        public void Init()
        {
            var options = new DbContextOptionsBuilder<ConvertedValueTestDbContext>()
                .UseInMemoryDatabase("ChangeTrackerConverter_" + Guid.NewGuid().ToString("N"))
                .Options;

            _db = new ConvertedValueTestDbContext(options);
        }

        [TestCleanup]
        public void Cleanup()
        {
            _db?.Dispose();
        }

        [TestMethod]
        public void Build_Added_ConverterMappedProperty_RecordsProviderValue()
        {
            var order = NewOrder();

            _db.Orders.Add(order);
            _db.ChangeTracker.DetectChanges();

            var entry = _db.ChangeTracker.Entries<ConvertedValueOrder>().Single();
            var result = ChangeTrackerEntryBuilder.Build(entry);

            Assert.AreEqual(AuditAction.Create, result.Action);

            var i18n = result.Properties.Single(p => p.PropertyName == "NameI18n");
            Assert.IsNull(i18n.OldValue);
            Assert.AreEqual(ConvertedValueTestDbContext.Serialize(order.NameI18n), i18n.NewValue);
            Assert.AreEqual("CONV|en=Name|ro=Nume", i18n.NewValue);
            Assert.IsFalse(i18n.NewValue.Contains("System.Collections"));
            Assert.IsFalse(i18n.NewValue.Contains("{"));

            var idProperty = result.Properties.Single(p => p.PropertyName == "Id");
            Assert.AreEqual(order.Id.ToString(), idProperty.NewValue);

            var nameProperty = result.Properties.Single(p => p.PropertyName == "CustomerName");
            Assert.AreEqual("Alice", nameProperty.NewValue);
        }

        [TestMethod]
        public void Build_Modified_UntouchedConverterMappedProperty_IsNotCaptured()
        {
            var order = NewOrder();

            _db.Orders.Add(order);
            _db.SaveChanges();
            _db.ChangeTracker.Clear();

            _db.Orders.Attach(order);
            order.CustomerName = "Bob";
            _db.ChangeTracker.DetectChanges();

            var entry = _db.ChangeTracker.Entries<ConvertedValueOrder>().Single();
            Assert.AreEqual(EntityState.Modified, entry.State);

            var result = ChangeTrackerEntryBuilder.Build(entry);

            Assert.AreEqual(1, result.Properties.Count);
            Assert.IsFalse(result.Properties.Any(p => p.PropertyName == "NameI18n"));

            var nameProperty = result.Properties.Single(p => p.PropertyName == "CustomerName");
            Assert.AreEqual("Alice", nameProperty.OldValue);
            Assert.AreEqual("Bob", nameProperty.NewValue);
        }

        [TestMethod]
        public void Build_Modified_ChangedConverterMappedProperty_RecordsBothProviderValues()
        {
            var order = NewOrder();
            var original = ConvertedValueTestDbContext.Serialize(order.NameI18n);

            _db.Orders.Add(order);
            _db.SaveChanges();
            _db.ChangeTracker.Clear();

            _db.Orders.Attach(order);
            order.NameI18n = new Dictionary<string, string> { { "en", "Renamed" }, { "ro", "Redenumit" } };
            _db.ChangeTracker.DetectChanges();

            var entry = _db.ChangeTracker.Entries<ConvertedValueOrder>().Single();
            var result = ChangeTrackerEntryBuilder.Build(entry);

            Assert.AreEqual(AuditAction.Update, result.Action);
            Assert.AreEqual(1, result.Properties.Count);

            var i18n = result.Properties.Single(p => p.PropertyName == "NameI18n");
            Assert.AreEqual(original, i18n.OldValue);
            Assert.AreEqual(ConvertedValueTestDbContext.Serialize(order.NameI18n), i18n.NewValue);
            Assert.AreEqual("CONV|en=Name|ro=Nume", i18n.OldValue);
            Assert.AreEqual("CONV|en=Renamed|ro=Redenumit", i18n.NewValue);
            Assert.AreNotEqual(i18n.OldValue, i18n.NewValue);
        }

        [TestMethod]
        public void Build_Deleted_ConverterMappedProperty_RecordsProviderValue()
        {
            var order = NewOrder();
            var original = ConvertedValueTestDbContext.Serialize(order.NameI18n);

            _db.Orders.Add(order);
            _db.SaveChanges();
            _db.ChangeTracker.Clear();

            _db.Orders.Attach(order);
            _db.Orders.Remove(order);

            var entry = _db.ChangeTracker.Entries<ConvertedValueOrder>().Single();
            Assert.AreEqual(EntityState.Deleted, entry.State);

            var result = ChangeTrackerEntryBuilder.Build(entry);

            Assert.AreEqual(AuditAction.Delete, result.Action);

            var i18n = result.Properties.Single(p => p.PropertyName == "NameI18n");
            Assert.AreEqual(original, i18n.OldValue);
            Assert.AreEqual("CONV|en=Name|ro=Nume", i18n.OldValue);
            Assert.IsNull(i18n.NewValue);
        }

        [TestMethod]
        public void Build_Added_ThrowingConverter_RecordsUnrecordableMarker()
        {
            var entity = new ThrowingConverterOrder { Id = Guid.NewGuid(), Payload = "payload-1" };

            _db.ThrowingOrders.Add(entity);
            _db.ChangeTracker.DetectChanges();

            var entry = _db.ChangeTracker.Entries<ThrowingConverterOrder>().Single();
            var result = ChangeTrackerEntryBuilder.Build(entry);

            Assert.AreEqual(2, result.Properties.Count);

            var payload = result.Properties.Single(p => p.PropertyName == "Payload");
            Assert.IsTrue(payload.NewValue.StartsWith("[unrecordable: converter threw "));
            Assert.IsFalse(payload.NewValue.Contains("payload-1"));

            var idProperty = result.Properties.Single(p => p.PropertyName == "Id");
            Assert.AreEqual(entity.Id.ToString(), idProperty.NewValue);
        }

        [TestMethod]
        public void GetValueConverter_ForConverterMappedProperty_ReturnsNonNull()
        {
            var property = _db.Model
                .FindEntityType(typeof(ConvertedValueOrder))
                .FindProperty(nameof(ConvertedValueOrder.NameI18n));

            Assert.IsNotNull(PropertyMetadataHelper.GetValueConverter(property));
        }

        private static ConvertedValueOrder NewOrder()
        {
            return new ConvertedValueOrder
            {
                Id = Guid.NewGuid(),
                CustomerName = "Alice",
                NameI18n = new Dictionary<string, string> { { "en", "Name" }, { "ro", "Nume" } }
            };
        }
    }
}
