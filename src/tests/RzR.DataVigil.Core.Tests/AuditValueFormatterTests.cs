using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RzR.DataVigil.Core.Helpers;

namespace RzR.DataVigil.Core.Tests
{
    [TestClass]
    public class AuditValueFormatterTests
    {
        #region Null

        [TestMethod]
        public void Format_Null_ReturnsNull()
        {
            Assert.IsNull(AuditValueFormatter.Format(null));
        }

        #endregion

        #region Enum

        [TestMethod]
        public void Format_Enum_ReturnsMemberName()
        {
            Assert.AreEqual("Green", AuditValueFormatter.Format(EdgeColor.Green));
        }

        [TestMethod]
        public void Format_BoxedNullableEnum_ReturnsMemberName()
        {
            EdgeColor? value = EdgeColor.Blue;
            object boxed = value;

            Assert.AreEqual("Blue", AuditValueFormatter.Format(boxed));
        }

        [TestMethod]
        public void Format_EnumInsideComplexValue_IsSerializedAsNumber()
        {
            var value = new EdgeContainerWithEnum { Color = EdgeColor.Green };

            Assert.AreEqual("{\"Color\":1}", AuditValueFormatter.Format(value));
        }

        #endregion

        #region IsSimpleType - primitives and SimpleTypeNames members

        [TestMethod]
        public void Format_BoxedNullableInt_ReturnsClrText()
        {
            int? value = 5;
            object boxed = value;

            Assert.AreEqual("5", AuditValueFormatter.Format(boxed));
        }

        [TestMethod]
        public void Format_String_ReturnsItself()
        {
            Assert.AreEqual("text", AuditValueFormatter.Format("text"));
        }

        [TestMethod]
        public void Format_Decimal_ReturnsClrText()
        {
            Assert.AreEqual(12.5m.ToString(), AuditValueFormatter.Format(12.5m));
        }

        [TestMethod]
        public void Format_Guid_ReturnsClrText()
        {
            var value = Guid.NewGuid();

            Assert.AreEqual(value.ToString(), AuditValueFormatter.Format(value));
        }

        [TestMethod]
        public void Format_DateTime_ReturnsClrText()
        {
            var value = new DateTime(2026, 9, 7, 13, 45, 0, DateTimeKind.Utc);

            Assert.AreEqual(value.ToString(), AuditValueFormatter.Format(value));
        }

        [TestMethod]
        public void Format_DateTimeOffset_ReturnsClrText()
        {
            var value = new DateTimeOffset(new DateTime(2026, 9, 7, 13, 45, 0, DateTimeKind.Utc), TimeSpan.Zero);

            Assert.AreEqual(value.ToString(), AuditValueFormatter.Format(value));
        }

        [TestMethod]
        public void Format_DateOnly_ReturnsClrText()
        {
            var value = new DateOnly(2026, 9, 7);

            Assert.AreEqual(value.ToString(), AuditValueFormatter.Format(value));
        }

        [TestMethod]
        public void Format_TimeOnly_ReturnsClrText()
        {
            var value = new TimeOnly(13, 45);

            Assert.AreEqual(value.ToString(), AuditValueFormatter.Format(value));
        }

        [TestMethod]
        public void Format_TimeSpan_ReturnsClrText()
        {
            var value = TimeSpan.FromMinutes(90);

            Assert.AreEqual(value.ToString(), AuditValueFormatter.Format(value));
        }

        [TestMethod]
        public void Format_Uri_ReturnsClrText()
        {
            var value = new Uri("https://example.org/audit");

            Assert.AreEqual(value.ToString(), AuditValueFormatter.Format(value));
        }

        [TestMethod]
        public void Format_Version_ReturnsClrText()
        {
            var value = new Version(1, 2, 3);

            Assert.AreEqual(value.ToString(), AuditValueFormatter.Format(value));
        }

        #endregion

        #region byte[]

        [TestMethod]
        public void Format_ByteArray_ReturnsSizeMarker()
        {
            var value = new byte[] { 1, 2, 3, 4, 5 };

            Assert.AreEqual("byte[5]", AuditValueFormatter.Format(value));
        }

        [TestMethod]
        public void Format_EmptyByteArray_ReturnsZeroSizeMarker()
        {
            Assert.AreEqual("byte[0]", AuditValueFormatter.Format(Array.Empty<byte>()));
        }

        #endregion

        #region ShouldSerializeAsJson - collections

        [TestMethod]
        public void Format_ListOfString_ReturnsJsonArray()
        {
            var value = new List<string> { "red", "green" };

            Assert.AreEqual("[\"red\",\"green\"]", AuditValueFormatter.Format(value));
        }

        [TestMethod]
        public void Format_StringArray_ReturnsJsonArray()
        {
            var value = new[] { "alpha", "beta" };

            Assert.AreEqual("[\"alpha\",\"beta\"]", AuditValueFormatter.Format(value));
        }

        [TestMethod]
        public void Format_DictionaryOfStringString_ReturnsJsonObject()
        {
            var value = new Dictionary<string, string> { { "en", "Name" }, { "ro", "Nume" } };

            Assert.AreEqual("{\"en\":\"Name\",\"ro\":\"Nume\"}", AuditValueFormatter.Format(value));
        }

        #endregion

        #region ShouldSerializeAsJson - types without ToString override

        [TestMethod]
        public void Format_ClassWithoutToStringOverride_ReturnsJsonObject()
        {
            var value = new EdgeWithoutToString { Name = "Alice", Value = 5 };

            Assert.AreEqual("{\"Name\":\"Alice\",\"Value\":5}", AuditValueFormatter.Format(value));
        }

        [TestMethod]
        public void Format_ClassWithToStringOverride_ReturnsToStringResult()
        {
            var value = new EdgeWithToString { Name = "Alice" };

            Assert.AreEqual("edge-Alice", AuditValueFormatter.Format(value));
        }

        [TestMethod]
        public void Format_StructWithToStringOverride_ReturnsToStringResult()
        {
            var value = new EdgeStructWithToString { X = 7 };

            Assert.AreEqual("struct-7", AuditValueFormatter.Format(value));
        }

        #endregion

        #region ShouldSerializeAsJson - IEnumerable with/without ToString override

        [TestMethod]
        public void Format_EnumerableWithToStringOverride_ReturnsToStringResult()
        {
            var value = new EdgeEnumerableWithToString();

            Assert.AreEqual("enumerable-with-tostring", AuditValueFormatter.Format(value));
        }

        [TestMethod]
        public void Format_EnumerableWithoutToStringOverride_ReturnsJsonArray()
        {
            var value = new EdgeEnumerableWithoutToString();

            Assert.AreEqual("[1,2]", AuditValueFormatter.Format(value));
        }

        #endregion

        #region Reference cycles and depth

        [TestMethod]
        public void Format_SelfReferencingGraph_IgnoresTheCycle()
        {
            var node = new EdgeNode { Name = "root" };
            node.Next = node;

            Assert.AreEqual("{\"Name\":\"root\",\"Next\":null}", AuditValueFormatter.Format(node));
        }

        [TestMethod]
        public void Format_ChainDeeperThanMaxDepth_IsUnrecordable()
        {
            EdgeChainNode head = null;
            for (var i = 0; i < 12; i++)
                head = new EdgeChainNode { Depth = i, Inner = head };

            var result = AuditValueFormatter.Format(head);

            Assert.IsTrue(result.StartsWith("[unrecordable: "),
                "A non-cyclic chain deeper than MaxDepth must be marked unrecordable, never silently truncated mid-object.");
        }

        [TestMethod]
        public void Format_PropertyGetterThrowsDuringSerialization_IsUnrecordable()
        {
            var result = AuditValueFormatter.Format(new EdgeThrowingNoToString());

            Assert.IsTrue(result.StartsWith("[unrecordable: "),
                "A type without ToString whose getter throws must be marked unrecordable, never leak an exception or fall back to ToString.");
            Assert.IsFalse(result.Contains("RzR.DataVigil"));
        }

        #endregion

        #region Truncation budget

        [TestMethod]
        public void Format_JsonExactlyAtMaxLength_IsReturnedUntruncated()
        {
            var payload = new EdgeSizedPayload { Data = new string('a', 7989) };

            var result = AuditValueFormatter.Format(payload);

            Assert.AreEqual(AuditValueFormatter.MaxJsonValueLength, result.Length);
            Assert.IsFalse(result.Contains("[truncated"));
            Assert.AreEqual("{\"Data\":\"" + new string('a', 7989) + "\"}", result);
        }

        [TestMethod]
        public void Format_JsonOneCharacterOverMaxLength_IsTruncatedWithContentAddressedMarker()
        {
            var payload = new EdgeSizedPayload { Data = new string('a', 7990) };
            var fullJson = "{\"Data\":\"" + new string('a', 7990) + "\"}";
            Assert.AreEqual(8001, fullJson.Length);

            var result = AuditValueFormatter.Format(payload);

            var expectedMarker = "...[truncated, full length " + fullJson.Length + ", sha256 " +
                                  ComputeSha256Prefix(fullJson) + "]";
            var expectedPrefixLength = AuditValueFormatter.MaxJsonValueLength - expectedMarker.Length;

            Assert.AreEqual(AuditValueFormatter.MaxJsonValueLength, result.Length);
            Assert.IsTrue(expectedMarker.Length >= 16, "sha256 prefix must be 16 hex characters long.");
            Assert.AreEqual(fullJson.Substring(0, expectedPrefixLength) + expectedMarker, result);
        }

        [TestMethod]
        public void Format_TruncatedValue_IsDeterministicAcrossCalls()
        {
            var payload = new EdgeSizedPayload { Data = new string('b', 9000) };

            var first = AuditValueFormatter.Format(payload);
            var second = AuditValueFormatter.Format(payload);

            Assert.AreEqual(first, second);
        }

        #endregion

        #region FormatKey

        [TestMethod]
        public void FormatKey_Null_ReturnsNull()
        {
            Assert.IsNull(AuditValueFormatter.FormatKey(null));
        }

        [TestMethod]
        public void FormatKey_ByteArray_ReturnsLowercaseHexWithoutSeparators()
        {
            var value = new byte[] { 0xAB, 0xCD, 0xEF, 0x01 };

            Assert.AreEqual("abcdef01", AuditValueFormatter.FormatKey(value));
        }

        [TestMethod]
        public void FormatKey_ConvertedGuidKeyBytes_StaysDistinctPerEntity()
        {
            var first = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6").ToByteArray();
            var second = Guid.Parse("11111111-2222-3333-4444-555555555555").ToByteArray();

            var firstFormatted = AuditValueFormatter.FormatKey(first);
            var secondFormatted = AuditValueFormatter.FormatKey(second);

            Assert.AreNotEqual(firstFormatted, secondFormatted);
            Assert.AreEqual(32, firstFormatted.Length);
            Assert.AreEqual(firstFormatted, firstFormatted.ToLowerInvariant());
        }

        [TestMethod]
        public void FormatKey_Guid_ReturnsClrText()
        {
            var value = Guid.NewGuid();

            Assert.AreEqual(value.ToString(), AuditValueFormatter.FormatKey(value));
        }

        [TestMethod]
        public void FormatKey_EnumKey_ReturnsMemberName()
        {
            Assert.AreEqual("Green", AuditValueFormatter.FormatKey(EdgeColor.Green));
        }

        [TestMethod]
        public void FormatKey_OversizedJsonValue_IsNeverTruncated()
        {
            var payload = new EdgeSizedPayload { Data = new string('a', 9000) };
            var fullJson = "{\"Data\":\"" + new string('a', 9000) + "\"}";

            var result = AuditValueFormatter.FormatKey(payload);

            Assert.AreEqual(fullJson, result);
            Assert.IsTrue(result.Length > AuditValueFormatter.MaxJsonValueLength);
            Assert.IsFalse(result.Contains("[truncated"));
        }

        #endregion

        #region NotMapped-style extra property

        [TestMethod]
        public void Format_ValueWithNotMappedExtraProperty_TheExtraPropertyIsStillIncluded()
        {
            var value = new EdgeValueWithNotMappedExtra { Name = "Alice", Computed = "derived" };

            var result = AuditValueFormatter.Format(value);

            Assert.AreEqual("{\"Name\":\"Alice\",\"Computed\":\"derived\"}", result);
        }

        #endregion

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

        private enum EdgeColor
        {
            Red = 0,
            Green = 1,
            Blue = 2
        }

        private class EdgeContainerWithEnum
        {
            public EdgeColor Color { get; set; }
        }

        private class EdgeWithoutToString
        {
            public string Name { get; set; }

            public int Value { get; set; }
        }

        private class EdgeWithToString
        {
            public string Name { get; set; }

            public override string ToString() => "edge-" + Name;
        }

        private struct EdgeStructWithToString
        {
            public int X { get; set; }

            public override string ToString() => "struct-" + X;
        }

        private class EdgeEnumerableWithToString : IEnumerable<int>
        {
            public IEnumerator<int> GetEnumerator()
            {
                yield return 1;
                yield return 2;
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

            public override string ToString() => "enumerable-with-tostring";
        }

        private class EdgeEnumerableWithoutToString : IEnumerable<int>
        {
            public IEnumerator<int> GetEnumerator()
            {
                yield return 1;
                yield return 2;
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        private class EdgeNode
        {
            public string Name { get; set; }

            public EdgeNode Next { get; set; }
        }

        private class EdgeChainNode
        {
            public int Depth { get; set; }

            public EdgeChainNode Inner { get; set; }
        }

        private class EdgeThrowingNoToString
        {
            public string Boom => throw new InvalidOperationException("boom");
        }

        private class EdgeSizedPayload
        {
            public string Data { get; set; }
        }

        private class EdgeValueWithNotMappedExtra
        {
            public string Name { get; set; }

            [NotMapped]
            public string Computed { get; set; }
        }
    }
}
