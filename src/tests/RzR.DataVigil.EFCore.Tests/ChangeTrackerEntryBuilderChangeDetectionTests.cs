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

namespace RzR.DataVigil.EFCore.Tests
{
    [TestClass]
    public class ChangeTrackerEntryBuilderChangeDetectionTests
    {
        private ConvertedValueTestDbContext _db;
        private ComplexValueTestDbContext _complexDb;

        [TestInitialize]
        public void Init()
        {
            var options = new DbContextOptionsBuilder<ConvertedValueTestDbContext>()
                .UseInMemoryDatabase("ChangeDetection_" + Guid.NewGuid().ToString("N"))
                .Options;
            _db = new ConvertedValueTestDbContext(options);

            var complexOptions = new DbContextOptionsBuilder<ComplexValueTestDbContext>()
                .UseInMemoryDatabase("ChangeDetectionComplex_" + Guid.NewGuid().ToString("N"))
                .Options;
            _complexDb = new ComplexValueTestDbContext(complexOptions);
        }

        [TestCleanup]
        public void Cleanup()
        {
            _db?.Dispose();
            _complexDb?.Dispose();
        }

        [TestMethod]
        public void Build_Added_GuidToByteArrayConverter_RecordsByteLength()
        {
            var order = NewOrder();
            order.SecretId = Guid.NewGuid();

            _db.Orders.Add(order);
            _db.ChangeTracker.DetectChanges();

            var entry = _db.ChangeTracker.Entries<ConvertedValueOrder>().Single();
            var result = ChangeTrackerEntryBuilder.Build(entry);

            var secretId = result.Properties.Single(p => p.PropertyName == nameof(ConvertedValueOrder.SecretId));
            Assert.AreEqual("byte[16]", secretId.NewValue);
            Assert.AreNotEqual(typeof(byte[]).ToString(), secretId.NewValue);
        }

        [TestMethod]
        public void Build_Modified_GuidToByteArrayConverterChangedValue_IsCaptured()
        {
            var order = NewOrder();
            order.SecretId = Guid.NewGuid();

            _db.Orders.Add(order);
            _db.SaveChanges();
            _db.ChangeTracker.Clear();

            _db.Orders.Attach(order);
            order.SecretId = Guid.NewGuid();
            _db.ChangeTracker.DetectChanges();

            var entry = _db.ChangeTracker.Entries<ConvertedValueOrder>().Single();
            Assert.AreEqual(EntityState.Modified, entry.State);

            var result = ChangeTrackerEntryBuilder.Build(entry);

            var secretId = result.Properties.Single(p => p.PropertyName == nameof(ConvertedValueOrder.SecretId));
            Assert.AreEqual("byte[16]", secretId.OldValue);
            Assert.AreEqual("byte[16]", secretId.NewValue);
        }

        [TestMethod]
        public void Build_Modified_NonDeterministicConverterUnchangedValue_IsNotCaptured()
        {
            var order = NewOrder();
            order.NonDeterministicTag = "steady";

            _db.Orders.Add(order);
            _db.SaveChanges();
            _db.ChangeTracker.Clear();

            _db.Orders.Attach(order);
            order.CustomerName = "Bob";
            _db.ChangeTracker.DetectChanges();

            var entry = _db.ChangeTracker.Entries<ConvertedValueOrder>().Single();
            Assert.AreEqual(EntityState.Modified, entry.State);

            var result = ChangeTrackerEntryBuilder.Build(entry);

            Assert.IsFalse(result.Properties.Any(p => p.PropertyName == nameof(ConvertedValueOrder.NonDeterministicTag)));
            Assert.IsTrue(result.Properties.Any(p => p.PropertyName == nameof(ConvertedValueOrder.CustomerName)));
        }

        [TestMethod]
        public void Build_Modified_ListDifferingOnlyPastJsonTruncationCut_IsCapturedWithDistinctMarkers()
        {
            var baseline = new List<string>();
            for (var i = 0; i < 1000; i++)
                baseline.Add("item-" + i.ToString("D4"));

            var original = new List<string>(baseline);
            var changed = new List<string>(baseline) { [baseline.Count - 1] = "item-ZZZZ" };

            var originalJson = JsonSerializer.Serialize(original, JsonOptions());
            var changedJson = JsonSerializer.Serialize(changed, JsonOptions());

            Assert.AreEqual(originalJson.Length, changedJson.Length);
            Assert.IsTrue(originalJson.Length > AuditValueFormatter.MaxJsonValueLength);
            var firstDiffIndex = FirstDifferenceIndex(originalJson, changedJson);
            Assert.IsTrue(firstDiffIndex > AuditValueFormatter.MaxJsonValueLength);

            var order = NewComplexOrder();
            order.Tags = original;

            _complexDb.Orders.Add(order);
            _complexDb.SaveChanges();
            _complexDb.ChangeTracker.Clear();

            _complexDb.Orders.Attach(order);
            order.Tags = changed;
            _complexDb.ChangeTracker.DetectChanges();

            var entry = _complexDb.ChangeTracker.Entries<ComplexValueOrder>().Single();
            Assert.AreEqual(EntityState.Modified, entry.State);

            var result = ChangeTrackerEntryBuilder.Build(entry);

            var tags = result.Properties.Single(p => p.PropertyName == nameof(ComplexValueOrder.Tags));
            Assert.IsNotNull(tags.OldValue);
            Assert.IsNotNull(tags.NewValue);
            Assert.AreNotEqual(tags.OldValue, tags.NewValue);

            Assert.AreEqual(AuditValueFormatter.MaxJsonValueLength, tags.OldValue.Length);
            Assert.AreEqual(AuditValueFormatter.MaxJsonValueLength, tags.NewValue.Length);

            var oldSha = ExtractShaMarker(tags.OldValue);
            var newSha = ExtractShaMarker(tags.NewValue);
            Assert.AreNotEqual(oldSha, newSha);
            Assert.AreEqual(ComputeSha256Prefix(originalJson), oldSha);
            Assert.AreEqual(ComputeSha256Prefix(changedJson), newSha);

            var oldFullLength = ExtractFullLength(tags.OldValue);
            var newFullLength = ExtractFullLength(tags.NewValue);
            Assert.AreEqual(oldFullLength, newFullLength);
            Assert.AreEqual(originalJson.Length.ToString(), oldFullLength);
        }

        [TestMethod]
        public void Build_Added_ThrowingConverter_RecordsUnrecordableMarker_NotModelValue()
        {
            var entity = new ThrowingConverterOrder { Id = Guid.NewGuid(), Payload = "top-secret-model-value" };

            _db.ThrowingOrders.Add(entity);
            _db.ChangeTracker.DetectChanges();

            var entry = _db.ChangeTracker.Entries<ThrowingConverterOrder>().Single();
            var result = ChangeTrackerEntryBuilder.Build(entry);

            var payload = result.Properties.Single(p => p.PropertyName == nameof(ThrowingConverterOrder.Payload));
            Assert.IsTrue(payload.NewValue.StartsWith("[unrecordable: converter threw "));
            Assert.IsFalse(payload.NewValue.Contains("top-secret-model-value"));
            Assert.IsFalse(payload.NewValue.Contains(entity.Payload));
        }

        [TestMethod]
        public void ToAuditValue_TypeOverridingToString_RecordsViaToStringNotJson()
        {
            var value = new ToStringOverrideValue("payload");

            var result = ChangeTrackerEntryBuilder.ToAuditValue(PlainProperty(), value);

            Assert.AreEqual("custom:payload", result);
            Assert.IsFalse(result.Contains("{"));
        }

        [TestMethod]
        public void ToAuditValue_TypeWithoutToStringOverride_RecordsAsJson()
        {
            var value = new PlainDataValue { Label = "payload" };

            var result = ChangeTrackerEntryBuilder.ToAuditValue(PlainProperty(), value);

            Assert.AreEqual("{\"Label\":\"payload\"}", result);
        }

        [TestMethod]
        public void Build_ConverterMappedPrimaryKey_EntityIdIsLowercaseHexOfProviderBytes()
        {
            var entity = new ConvertedKeyOrder { Id = Guid.NewGuid(), Description = "key-test" };

            _db.KeyOrders.Add(entity);
            _db.ChangeTracker.DetectChanges();

            var entry = _db.ChangeTracker.Entries<ConvertedKeyOrder>().Single();
            var result = ChangeTrackerEntryBuilder.Build(entry);

            var expectedHex = BitConverter.ToString(entity.Id.ToByteArray()).Replace("-", string.Empty).ToLowerInvariant();
            Assert.AreEqual(expectedHex, result.EntityId);
            Assert.AreEqual(32, result.EntityId.Length);
            Assert.AreNotEqual(entity.Id.ToString(), result.EntityId);
            Assert.AreNotEqual("byte[16]", result.EntityId);
        }

        private object PlainProperty()
        {
            return _db.Model
                .FindEntityType(typeof(ConvertedValueOrder))
                .FindProperty(nameof(ConvertedValueOrder.CustomerName));
        }

        private static ConvertedValueOrder NewOrder()
        {
            return new ConvertedValueOrder
            {
                Id = Guid.NewGuid(),
                CustomerName = "Alice",
                NameI18n = new Dictionary<string, string> { { "en", "Name" } }
            };
        }

        private static ComplexValueOrder NewComplexOrder()
        {
            return new ComplexValueOrder
            {
                Id = Guid.NewGuid(),
                CustomerName = "Alice",
                Status = ComplexValueOrderStatus.Draft
            };
        }

        private static JsonSerializerOptions JsonOptions()
        {
            return new JsonSerializerOptions
            {
                MaxDepth = 8,
                ReferenceHandler = ReferenceHandler.IgnoreCycles
            };
        }

        private static int FirstDifferenceIndex(string a, string b)
        {
            var length = Math.Min(a.Length, b.Length);
            for (var i = 0; i < length; i++)
                if (a[i] != b[i])
                    return i;

            return length;
        }

        private static string ExtractShaMarker(string recorded)
        {
            const string marker = "sha256 ";
            var index = recorded.IndexOf(marker, StringComparison.Ordinal);
            return recorded.Substring(index + marker.Length, 16);
        }

        private static string ExtractFullLength(string recorded)
        {
            const string marker = "full length ";
            var start = recorded.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
            var end = recorded.IndexOf(',', start);
            return recorded.Substring(start, end - start);
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

        private class ToStringOverrideValue
        {
            private readonly string _text;

            public ToStringOverrideValue(string text)
            {
                _text = text;
            }

            public override string ToString()
            {
                return "custom:" + _text;
            }
        }

        private class PlainDataValue
        {
            public string Label { get; set; }
        }
    }
}
