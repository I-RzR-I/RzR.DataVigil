using System;
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
    public class ChangeTrackerEntryBuilderTests
    {
        private TestDbContext _db;

        [TestInitialize]
        public void Init()
        {
            var options = new DbContextOptionsBuilder<TestDbContext>()
                .UseInMemoryDatabase("ChangeTracker_" + Guid.NewGuid().ToString("N"))
                .Options;

            _db = new TestDbContext(options);
        }

        [TestCleanup]
        public void Cleanup()
        {
            _db?.Dispose();
        }

        [TestMethod]
        public void Build_Added_AllProperties_HaveNullOldValue_And_CorrectNewValue()
        {
            var id = Guid.NewGuid();
            var order = new TestOrder
            {
                Id = id,
                CustomerName = "Alice",
                Total = 99.95m,
                ShippedAt = new DateTime(2026, 4, 15, 10, 0, 0, DateTimeKind.Utc),
                Quantity = 3
            };

            _db.Orders.Add(order);
            _db.ChangeTracker.DetectChanges();

            var entry = _db.ChangeTracker.Entries<TestOrder>().Single();
            var result = ChangeTrackerEntryBuilder.Build(entry);

            Assert.AreEqual(AuditAction.Create, result.Action);
            Assert.AreEqual("TestOrder", result.EntityName);
            Assert.AreEqual(id.ToString(), result.EntityId);
            Assert.IsTrue(result.Properties.Count >= 5);

            var idProp = result.Properties.Single(p => p.PropertyName == "Id");
            Assert.IsNull(idProp.OldValue);
            Assert.AreEqual(id.ToString(), idProp.NewValue);
            Assert.AreEqual("System.Guid", idProp.PropertyType);

            var nameProp = result.Properties.Single(p => p.PropertyName == "CustomerName");
            Assert.IsNull(nameProp.OldValue);
            Assert.AreEqual("Alice", nameProp.NewValue);
            Assert.AreEqual("System.String", nameProp.PropertyType);

            var totalProp = result.Properties.Single(p => p.PropertyName == "Total");
            Assert.IsNull(totalProp.OldValue);
            Assert.AreEqual("99.95", totalProp.NewValue);
            Assert.AreEqual("System.Decimal", totalProp.PropertyType);

            var shippedProp = result.Properties.Single(p => p.PropertyName == "ShippedAt");
            Assert.IsNull(shippedProp.OldValue);
            Assert.IsNotNull(shippedProp.NewValue);
            Assert.AreEqual("System.DateTime?", shippedProp.PropertyType);

            var qtyProp = result.Properties.Single(p => p.PropertyName == "Quantity");
            Assert.IsNull(qtyProp.OldValue);
            Assert.AreEqual("3", qtyProp.NewValue);
            Assert.AreEqual("System.Int32", qtyProp.PropertyType);
        }

        [TestMethod]
        public void Build_Added_NullablePropertyIsNull_NewValueIsNull()
        {
            var order = new TestOrder
            {
                Id = Guid.NewGuid(),
                CustomerName = "Bob",
                Total = 0m,
                ShippedAt = null,
                Quantity = 0
            };

            _db.Orders.Add(order);
            _db.ChangeTracker.DetectChanges();

            var entry = _db.ChangeTracker.Entries<TestOrder>().Single();
            var result = ChangeTrackerEntryBuilder.Build(entry);

            var shippedProp = result.Properties.Single(p => p.PropertyName == "ShippedAt");
            Assert.IsNull(shippedProp.OldValue);
            Assert.IsNull(shippedProp.NewValue);
            Assert.AreEqual("System.DateTime?", shippedProp.PropertyType);
        }

        [TestMethod]
        public void Build_Modified_OnlyChangedProperties_AreIncluded()
        {
            var order = new TestOrder
            {
                Id = Guid.NewGuid(),
                CustomerName = "Alice",
                Total = 50m,
                ShippedAt = null,
                Quantity = 1
            };

            _db.Orders.Add(order);
            _db.SaveChanges();
            _db.ChangeTracker.Clear();

            _db.Orders.Attach(order);
            order.CustomerName = "Bob";
            order.Total = 75.50m;
            _db.ChangeTracker.DetectChanges();

            var entry = _db.ChangeTracker.Entries<TestOrder>().Single();
            Assert.AreEqual(EntityState.Modified, entry.State);

            var result = ChangeTrackerEntryBuilder.Build(entry);

            Assert.AreEqual(AuditAction.Update, result.Action);

            // Only CustomerName and Total changed
            Assert.AreEqual(2, result.Properties.Count);

            var nameProp = result.Properties.Single(p => p.PropertyName == "CustomerName");
            Assert.AreEqual("Alice", nameProp.OldValue);
            Assert.AreEqual("Bob", nameProp.NewValue);
            Assert.AreEqual("System.String", nameProp.PropertyType);

            var totalProp = result.Properties.Single(p => p.PropertyName == "Total");
            Assert.AreEqual("50", totalProp.OldValue);
            Assert.AreEqual("75.50", totalProp.NewValue);
            Assert.AreEqual("System.Decimal", totalProp.PropertyType);
        }

        [TestMethod]
        public void Build_Modified_NullToValue_RecordsCorrectOldAndNew()
        {
            var order = new TestOrder
            {
                Id = Guid.NewGuid(),
                CustomerName = "Carol",
                Total = 10m,
                ShippedAt = null,
                Quantity = 1
            };

            _db.Orders.Add(order);
            _db.SaveChanges();
            _db.ChangeTracker.Clear();

            var shipped = new DateTime(2026, 4, 15, 12, 0, 0, DateTimeKind.Utc);
            _db.Orders.Attach(order);
            order.ShippedAt = shipped;
            _db.ChangeTracker.DetectChanges();

            var entry = _db.ChangeTracker.Entries<TestOrder>().Single();
            var result = ChangeTrackerEntryBuilder.Build(entry);

            Assert.AreEqual(1, result.Properties.Count);

            var shippedProp = result.Properties.Single(p => p.PropertyName == "ShippedAt");
            Assert.IsNull(shippedProp.OldValue);
            Assert.AreEqual(shipped.ToString(), shippedProp.NewValue);
            Assert.AreEqual("System.DateTime?", shippedProp.PropertyType);
        }

        [TestMethod]
        public void Build_Modified_ValueToNull_RecordsCorrectOldAndNew()
        {
            var shipped = new DateTime(2026, 4, 10, 8, 0, 0, DateTimeKind.Utc);
            var order = new TestOrder
            {
                Id = Guid.NewGuid(),
                CustomerName = "Dave",
                Total = 20m,
                ShippedAt = shipped,
                Quantity = 2
            };

            _db.Orders.Add(order);
            _db.SaveChanges();
            _db.ChangeTracker.Clear();

            _db.Orders.Attach(order);
            order.ShippedAt = null;
            _db.ChangeTracker.DetectChanges();

            var entry = _db.ChangeTracker.Entries<TestOrder>().Single();
            var result = ChangeTrackerEntryBuilder.Build(entry);

            var shippedProp = result.Properties.Single(p => p.PropertyName == "ShippedAt");
            Assert.AreEqual(shipped.ToString(), shippedProp.OldValue);
            Assert.IsNull(shippedProp.NewValue);
        }

        [TestMethod]
        public void Build_Modified_NoChanges_PropertiesEmpty()
        {
            var order = new TestOrder
            {
                Id = Guid.NewGuid(),
                CustomerName = "Eve",
                Total = 30m,
                ShippedAt = null,
                Quantity = 5
            };

            _db.Orders.Add(order);
            _db.SaveChanges();
            _db.ChangeTracker.Clear();

            // Re-attach without modifications — force Modified state manually
            _db.Orders.Attach(order);
            _db.Entry(order).State = EntityState.Modified;
            _db.ChangeTracker.DetectChanges();

            var entry = _db.ChangeTracker.Entries<TestOrder>().Single();
            var result = ChangeTrackerEntryBuilder.Build(entry);

            // All property values are still the same, so no properties should be included
            Assert.AreEqual(0, result.Properties.Count);
        }

        [TestMethod]
        public void Build_Deleted_AllProperties_HaveNullNewValue_And_CorrectOldValue()
        {
            var id = Guid.NewGuid();
            var shipped = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
            var order = new TestOrder
            {
                Id = id,
                CustomerName = "Frank",
                Total = 199.99m,
                ShippedAt = shipped,
                Quantity = 7
            };

            _db.Orders.Add(order);
            _db.SaveChanges();
            _db.ChangeTracker.Clear();

            _db.Orders.Attach(order);
            _db.Orders.Remove(order);
            _db.ChangeTracker.DetectChanges();

            var entry = _db.ChangeTracker.Entries<TestOrder>().Single();
            Assert.AreEqual(EntityState.Deleted, entry.State);

            var result = ChangeTrackerEntryBuilder.Build(entry);

            Assert.AreEqual(AuditAction.Delete, result.Action);
            Assert.AreEqual("TestOrder", result.EntityName);
            Assert.AreEqual(id.ToString(), result.EntityId);
            Assert.IsTrue(result.Properties.Count >= 5);

            var idProp = result.Properties.Single(p => p.PropertyName == "Id");
            Assert.AreEqual(id.ToString(), idProp.OldValue);
            Assert.IsNull(idProp.NewValue);

            var nameProp = result.Properties.Single(p => p.PropertyName == "CustomerName");
            Assert.AreEqual("Frank", nameProp.OldValue);
            Assert.IsNull(nameProp.NewValue);

            var totalProp = result.Properties.Single(p => p.PropertyName == "Total");
            Assert.AreEqual("199.99", totalProp.OldValue);
            Assert.IsNull(totalProp.NewValue);

            var shippedProp = result.Properties.Single(p => p.PropertyName == "ShippedAt");
            Assert.AreEqual(shipped.ToString(), shippedProp.OldValue);
            Assert.IsNull(shippedProp.NewValue);
            Assert.AreEqual("System.DateTime?", shippedProp.PropertyType);

            var qtyProp = result.Properties.Single(p => p.PropertyName == "Quantity");
            Assert.AreEqual("7", qtyProp.OldValue);
            Assert.IsNull(qtyProp.NewValue);
        }

        [TestMethod]
        public void Build_Deleted_NullablePropertyIsNull_OldValueIsNull()
        {
            var order = new TestOrder
            {
                Id = Guid.NewGuid(),
                CustomerName = "Grace",
                Total = 0m,
                ShippedAt = null,
                Quantity = 0
            };

            _db.Orders.Add(order);
            _db.SaveChanges();
            _db.ChangeTracker.Clear();

            _db.Orders.Attach(order);
            _db.Orders.Remove(order);
            _db.ChangeTracker.DetectChanges();

            var entry = _db.ChangeTracker.Entries<TestOrder>().Single();
            var result = ChangeTrackerEntryBuilder.Build(entry);

            var shippedProp = result.Properties.Single(p => p.PropertyName == "ShippedAt");
            Assert.IsNull(shippedProp.OldValue);
            Assert.IsNull(shippedProp.NewValue);
            Assert.AreEqual("System.DateTime?", shippedProp.PropertyType);
        }

        [TestMethod]
        public void Build_PropertyType_NonNullable_StoresCleanFullName()
        {
            var order = new TestOrder
            {
                Id = Guid.NewGuid(),
                CustomerName = "Hank",
                Total = 1m,
                ShippedAt = null,
                Quantity = 1
            };

            _db.Orders.Add(order);
            _db.ChangeTracker.DetectChanges();

            var entry = _db.ChangeTracker.Entries<TestOrder>().Single();
            var result = ChangeTrackerEntryBuilder.Build(entry);

            Assert.AreEqual("System.Guid", result.Properties.Single(p => p.PropertyName == "Id").PropertyType);
            Assert.AreEqual("System.String", result.Properties.Single(p => p.PropertyName == "CustomerName").PropertyType);
            Assert.AreEqual("System.Decimal", result.Properties.Single(p => p.PropertyName == "Total").PropertyType);
            Assert.AreEqual("System.Int32", result.Properties.Single(p => p.PropertyName == "Quantity").PropertyType);
        }

        [TestMethod]
        public void Build_PropertyType_Nullable_StoresCleanNameWithQuestionMark()
        {
            var order = new TestOrder
            {
                Id = Guid.NewGuid(),
                CustomerName = "Ivy",
                Total = 1m,
                ShippedAt = DateTime.UtcNow,
                Quantity = 1
            };

            _db.Orders.Add(order);
            _db.ChangeTracker.DetectChanges();

            var entry = _db.ChangeTracker.Entries<TestOrder>().Single();
            var result = ChangeTrackerEntryBuilder.Build(entry);

            var shippedProp = result.Properties.Single(p => p.PropertyName == "ShippedAt");
            Assert.AreEqual("System.DateTime?", shippedProp.PropertyType);
            Assert.IsFalse(shippedProp.PropertyType.Contains("Nullable"));
            Assert.IsFalse(shippedProp.PropertyType.Contains("CoreLib"));
        }

        [TestMethod]
        public void Build_EntityId_MatchesPrimaryKeyValue()
        {
            var id = Guid.NewGuid();
            var order = new TestOrder
            {
                Id = id,
                CustomerName = "Jake",
                Total = 1m,
                Quantity = 1
            };

            _db.Orders.Add(order);
            _db.ChangeTracker.DetectChanges();

            var entry = _db.ChangeTracker.Entries<TestOrder>().Single();
            var result = ChangeTrackerEntryBuilder.Build(entry);

            Assert.AreEqual(id.ToString(), result.EntityId);
        }

        [TestMethod]
        public void Build_EntityTypeName_ContainsFullNamespace()
        {
            var order = new TestOrder
            {
                Id = Guid.NewGuid(),
                CustomerName = "Kate",
                Total = 1m,
                Quantity = 1
            };

            _db.Orders.Add(order);
            _db.ChangeTracker.DetectChanges();

            var entry = _db.ChangeTracker.Entries<TestOrder>().Single();
            var result = ChangeTrackerEntryBuilder.Build(entry);

            Assert.AreEqual(typeof(TestOrder).FullName, result.EntityTypeName);
        }
    }
}
