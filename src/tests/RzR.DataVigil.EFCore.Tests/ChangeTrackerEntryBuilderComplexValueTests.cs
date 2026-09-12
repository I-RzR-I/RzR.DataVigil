using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RzR.DataVigil.Core.Helpers;
using RzR.DataVigil.EFCore.Helpers;
using RzR.DataVigil.EFCore.Tests.Data;
using RzR.DataVigil.EFCore.Tests.Entities;
using RzR.DataVigil.EFCore.Tests.Helpers;

namespace RzR.DataVigil.EFCore.Tests
{
    [TestClass]
    public class ChangeTrackerEntryBuilderComplexValueTests
    {
        private ComplexValueTestDbContext _db;

        [TestInitialize]
        public void Init()
        {
            var options = new DbContextOptionsBuilder<ComplexValueTestDbContext>()
                .UseInMemoryDatabase("ChangeTrackerComplexValue_" + Guid.NewGuid().ToString("N"))
                .Options;

            _db = new ComplexValueTestDbContext(options);
        }

        [TestCleanup]
        public void Cleanup()
        {
            _db?.Dispose();
        }

        [TestMethod]
        public void Build_Added_EnumWithNumericConversion_RecordsMemberName()
        {
            var order = NewOrder();
            order.Status = ComplexValueOrderStatus.Submitted;

            _db.Orders.Add(order);
            _db.ChangeTracker.DetectChanges();

            var entry = _db.ChangeTracker.Entries<ComplexValueOrder>().Single();
            var result = ChangeTrackerEntryBuilder.Build(entry);

            var status = result.Properties.Single(p => p.PropertyName == nameof(ComplexValueOrder.Status));
            Assert.AreEqual("Submitted", status.NewValue);
        }

        [TestMethod]
        public void Build_Added_PrimitiveCollectionProperty_RecordsJsonArray()
        {
            var order = NewOrder();
            order.Tags = new List<string> { "red", "green" };

            _db.Orders.Add(order);
            _db.ChangeTracker.DetectChanges();

            var entry = _db.ChangeTracker.Entries<ComplexValueOrder>().Single();
            var result = ChangeTrackerEntryBuilder.Build(entry);

            Assert.IsNull(entry.Property(nameof(ComplexValueOrder.Tags)).Metadata.GetValueConverter());

            var tags = result.Properties.Single(p => p.PropertyName == nameof(ComplexValueOrder.Tags));
            Assert.AreEqual("[\"red\",\"green\"]", tags.NewValue);
        }

        [TestMethod]
        public void Build_Added_BinaryProperty_RecordsSize()
        {
            var order = NewOrder();
            order.RowVersion = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };

            _db.Orders.Add(order);
            _db.ChangeTracker.DetectChanges();

            var entry = _db.ChangeTracker.Entries<ComplexValueOrder>().Single();
            var result = ChangeTrackerEntryBuilder.Build(entry);

            var rowVersion = result.Properties.Single(p => p.PropertyName == nameof(ComplexValueOrder.RowVersion));
            Assert.AreEqual("byte[8]", rowVersion.NewValue);
        }

        [TestMethod]
        public void Build_Modified_ByteArrayOfEqualLength_IsCaptured()
        {
            var order = NewOrder();
            order.RowVersion = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };

            _db.Orders.Add(order);
            _db.SaveChanges();
            _db.ChangeTracker.Clear();

            _db.Orders.Attach(order);
            order.CustomerName = "Bob";
            order.RowVersion = new byte[] { 9, 9, 9, 9, 9, 9, 9, 9 };
            _db.ChangeTracker.DetectChanges();

            var entry = _db.ChangeTracker.Entries<ComplexValueOrder>().Single();
            var result = ChangeTrackerEntryBuilder.Build(entry);

            var rowVersion = result.Properties.Single(p => p.PropertyName == nameof(ComplexValueOrder.RowVersion));
            Assert.AreEqual("byte[8]", rowVersion.OldValue);
            Assert.AreEqual("byte[8]", rowVersion.NewValue);

            var customerName = result.Properties.Single(p => p.PropertyName == nameof(ComplexValueOrder.CustomerName));
            Assert.AreEqual("Bob", customerName.NewValue);
        }

        [TestMethod]
        public void ToAuditValue_NullValue_RecordsNull()
        {
            Assert.IsNull(ChangeTrackerEntryBuilder.ToAuditValue(PlainProperty(), null));
        }

        [TestMethod]
        public void ToAuditValue_ListWithoutConverter_RecordsJsonArray()
        {
            var value = new List<string> { "alpha", "beta" };

            Assert.AreEqual("[\"alpha\",\"beta\"]", ChangeTrackerEntryBuilder.ToAuditValue(PlainProperty(), value));
        }

        [TestMethod]
        public void ToAuditValue_ArrayWithoutConverter_RecordsJsonArray()
        {
            var value = new[] { 3, 5, 8 };

            Assert.AreEqual("[3,5,8]", ChangeTrackerEntryBuilder.ToAuditValue(PlainProperty(), value));
        }

        [TestMethod]
        public void ToAuditValue_PlainClassWithoutConverter_RecordsJsonObject()
        {
            var value = new ComplexValueAddress { City = "Chisinau", Street = "Stefan cel Mare 1" };

            Assert.AreEqual(
                "{\"City\":\"Chisinau\",\"Street\":\"Stefan cel Mare 1\"}",
                ChangeTrackerEntryBuilder.ToAuditValue(PlainProperty(), value));
        }

        [TestMethod]
        public void ToAuditValue_StructWithoutConverter_RecordsJsonObject()
        {
            var value = new ComplexValueAmount { Value = 12.5m, Currency = "MDL" };

            Assert.AreEqual(
                "{\"Value\":12.5,\"Currency\":\"MDL\"}",
                ChangeTrackerEntryBuilder.ToAuditValue(PlainProperty(), value));
        }

        [TestMethod]
        public void ToAuditValue_NullableEnumValue_RecordsMemberName()
        {
            ComplexValueOrderStatus? value = ComplexValueOrderStatus.Archived;

            Assert.AreEqual("Archived", ChangeTrackerEntryBuilder.ToAuditValue(PlainProperty(), value));
        }

        [TestMethod]
        public void ToAuditValue_SimpleTypes_RecordsClrText()
        {
            var property = PlainProperty();
            var guid = Guid.NewGuid();
            var moment = new DateTime(2026, 9, 7, 13, 45, 0, DateTimeKind.Utc);
            var offset = new DateTimeOffset(moment, TimeSpan.Zero);
            var day = new DateOnly(2026, 9, 7);
            var time = new TimeOnly(13, 45);
            var span = TimeSpan.FromMinutes(90);
            var uri = new Uri("https://example.org/audit");
            var version = new Version(1, 2, 3);

            Assert.AreEqual("42", ChangeTrackerEntryBuilder.ToAuditValue(property, 42));
            Assert.AreEqual("True", ChangeTrackerEntryBuilder.ToAuditValue(property, true));
            Assert.AreEqual("a", ChangeTrackerEntryBuilder.ToAuditValue(property, 'a'));
            Assert.AreEqual("text", ChangeTrackerEntryBuilder.ToAuditValue(property, "text"));
            Assert.AreEqual(12.5m.ToString(), ChangeTrackerEntryBuilder.ToAuditValue(property, 12.5m));
            Assert.AreEqual(guid.ToString(), ChangeTrackerEntryBuilder.ToAuditValue(property, guid));
            Assert.AreEqual(moment.ToString(), ChangeTrackerEntryBuilder.ToAuditValue(property, moment));
            Assert.AreEqual(offset.ToString(), ChangeTrackerEntryBuilder.ToAuditValue(property, offset));
            Assert.AreEqual(day.ToString(), ChangeTrackerEntryBuilder.ToAuditValue(property, day));
            Assert.AreEqual(time.ToString(), ChangeTrackerEntryBuilder.ToAuditValue(property, time));
            Assert.AreEqual(span.ToString(), ChangeTrackerEntryBuilder.ToAuditValue(property, span));
            Assert.AreEqual(uri.ToString(), ChangeTrackerEntryBuilder.ToAuditValue(property, uri));
            Assert.AreEqual(version.ToString(), ChangeTrackerEntryBuilder.ToAuditValue(property, version));
        }

        [TestMethod]
        public void ToAuditValue_ByteArrayWithoutConverter_RecordsSize()
        {
            var value = new byte[] { 10, 20, 30 };

            Assert.AreEqual("byte[3]", ChangeTrackerEntryBuilder.ToAuditValue(PlainProperty(), value));
        }

        [TestMethod]
        public void ToAuditValue_SelfReferencingGraph_RecordsJsonWithoutRecursion()
        {
            var node = new ComplexValueNode { Name = "root" };
            node.Next = node;

            var result = ChangeTrackerEntryBuilder.ToAuditValue(PlainProperty(), node);

            Assert.AreEqual("{\"Name\":\"root\",\"Next\":null}", result);
        }

        [TestMethod]
        public void ToAuditValue_MutuallyReferencingGraph_RecordsJsonWithoutRecursion()
        {
            var first = new ComplexValueNode { Name = "first" };
            var second = new ComplexValueNode { Name = "second", Next = first };
            first.Next = second;

            var result = ChangeTrackerEntryBuilder.ToAuditValue(PlainProperty(), first);

            Assert.AreEqual("{\"Name\":\"first\",\"Next\":{\"Name\":\"second\",\"Next\":null}}", result);
        }

        [TestMethod]
        public void ToAuditValue_OversizedGraph_TruncatesVisibly()
        {
            var value = new List<string>();
            for (var i = 0; i < 2000; i++)
                value.Add("item-value-" + i);

            var result = ChangeTrackerEntryBuilder.ToAuditValue(PlainProperty(), value);

            var fullJson = JsonSerializer.Serialize(value, new JsonSerializerOptions
            {
                MaxDepth = 8,
                ReferenceHandler = ReferenceHandler.IgnoreCycles
            });
            var expectedMarker = "...[truncated, full length " + fullJson.Length + ", sha256 " +
                                  ComputeSha256Prefix(fullJson) + "]";

            Assert.AreEqual(AuditValueFormatter.MaxJsonValueLength, result.Length);
            Assert.IsTrue(result.StartsWith("[\"item-value-0\","));
            Assert.IsTrue(result.EndsWith(expectedMarker));
            Assert.AreEqual(result, ChangeTrackerEntryBuilder.ToAuditValue(PlainProperty(), value));
        }

        private static string ComputeSha256Prefix(string value)
        {
            using (var sha256 = SHA256.Create())
            {
                var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(value));
                var builder = new StringBuilder(16);

                for (var i = 0; i < 8; i++)
                    builder.Append(hash[i].ToString("x2"));

                return builder.ToString();
            }
        }

        [TestMethod]
        public void ToAuditValue_SerializationFailure_FallsBackToString()
        {
            var result = ChangeTrackerEntryBuilder.ToAuditValue(PlainProperty(), new ComplexValueExploding());

            Assert.AreEqual("exploding-value", result);
        }

        private object PlainProperty()
        {
            return _db.Model
                .FindEntityType(typeof(ComplexValueOrder))
                .FindProperty(nameof(ComplexValueOrder.CustomerName));
        }

        private static ComplexValueOrder NewOrder()
        {
            return new ComplexValueOrder
            {
                Id = Guid.NewGuid(),
                CustomerName = "Alice",
                Status = ComplexValueOrderStatus.Draft
            };
        }
    }
}
