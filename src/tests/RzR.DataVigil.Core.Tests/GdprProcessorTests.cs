using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RzR.DataVigil.Abstractions.Enums;
using RzR.DataVigil.Abstractions.Models.Gdpr;
using RzR.DataVigil.Core.Gdpr;
using static RzR.DataVigil.Core.Tests.Helpers.AuditTestDataBuilder;
using static RzR.DataVigil.Core.Tests.Helpers.GdprPolicyRegistryHelper;

namespace RzR.DataVigil.Core.Tests
{
    [TestClass]
    public class GdprProcessorTests
    {
        [TestMethod]
        public void ApplyStoragePolicies_WithNoPolicy_ReturnsFalse()
        {
            var processor = new GdprProcessor(new GdprPolicyRegistry());
            var entry = BuildEntryWithProperties("Order", Prop("Name", "Old", "New"));

            var (result, applied, _) = processor.ApplyStoragePolicies(entry);

            Assert.IsFalse(applied);
            Assert.AreEqual("Old", result.Properties.First().OldValue);
        }

        [TestMethod]
        public void ApplyStoragePolicies_WithEmptyStorageRules_ReturnsFalse()
        {
            var policy = new EntityGdprPolicy { StorageRules = Array.Empty<FieldGdprRule>() };
            var registry = CreateRegistry("Order", policy);
            var processor = new GdprProcessor(registry);
            var entry = BuildEntryWithProperties("Order", Prop("Name", "Old", "New"));

            var (_, applied, _) = processor.ApplyStoragePolicies(entry);

            Assert.IsFalse(applied);
        }

        [TestMethod]
        public void ApplyStoragePolicies_MaskAction_MasksValues()
        {
            var policy = new EntityGdprPolicy
            {
                StorageRules = new[] { new FieldGdprRule { FieldName = "Email", Action = GdprFieldAction.Mask } }
            };
            var registry = CreateRegistry("Order", policy);
            var processor = new GdprProcessor(registry);
            var entry = BuildEntryWithProperties("Order", Prop("Email", "old@test.com", "new@test.com"));

            var (result, applied, _) = processor.ApplyStoragePolicies(entry);

            Assert.IsTrue(applied);

            var prop = result.Properties.First();
            Assert.IsTrue(prop.OldValue.Contains("*"), "Old value should be masked");
            Assert.IsTrue(prop.NewValue.Contains("*"), "New value should be masked");
            Assert.AreEqual('o', prop.OldValue[0]);
            Assert.AreEqual('m', prop.OldValue[prop.OldValue.Length - 1]);
        }

        [TestMethod]
        public void ApplyStoragePolicies_MaskAction_ShortValue_ReturnsStars()
        {
            var policy = new EntityGdprPolicy
            {
                StorageRules = new[] { new FieldGdprRule { FieldName = "Code", Action = GdprFieldAction.Mask } }
            };
            var registry = CreateRegistry("Order", policy);
            var processor = new GdprProcessor(registry);
            var entry = BuildEntryWithProperties("Order", Prop("Code", "AB", "X"));

            var (result, _, _) = processor.ApplyStoragePolicies(entry);

            Assert.AreEqual("***", result.Properties.First().OldValue);
            Assert.AreEqual("***", result.Properties.First().NewValue);
        }

        [TestMethod]
        public void ApplyStoragePolicies_MaskAction_NullValue_ReturnsNull()
        {
            var policy = new EntityGdprPolicy
            {
                StorageRules = new[] { new FieldGdprRule { FieldName = "Email", Action = GdprFieldAction.Mask } }
            };
            var registry = CreateRegistry("Order", policy);
            var processor = new GdprProcessor(registry);
            var entry = BuildEntryWithProperties("Order", Prop("Email", null, "new@test.com"));

            var (result, _, _) = processor.ApplyStoragePolicies(entry);

            Assert.IsNull(result.Properties.First().OldValue);
        }

        [TestMethod]
        public void ApplyStoragePolicies_HashAction_ProducesSha256Hex()
        {
            var policy = new EntityGdprPolicy
            {
                StorageRules = new[] { new FieldGdprRule { FieldName = "SSN", Action = GdprFieldAction.Hash } }
            };
            var registry = CreateRegistry("Order", policy);
            var processor = new GdprProcessor(registry);
            var entry = BuildEntryWithProperties("Order", Prop("SSN", "123-45-6789", null));

            var (result, applied, _) = processor.ApplyStoragePolicies(entry);

            Assert.IsTrue(applied);
            var prop = result.Properties.First();
            Assert.AreEqual(64, prop.OldValue.Length, "SHA256 hex should be 64 chars");
            Assert.IsNull(prop.NewValue);

            // Verify deterministic
            using var sha = SHA256.Create();
            var expected = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes("123-45-6789")))
                .Replace("-", "").ToLower();
            Assert.AreEqual(expected, prop.OldValue);
        }

        [TestMethod]
        public void ApplyStoragePolicies_HashAction_NullValue_ReturnsNull()
        {
            var policy = new EntityGdprPolicy
            {
                StorageRules = new[] { new FieldGdprRule { FieldName = "SSN", Action = GdprFieldAction.Hash } }
            };
            var registry = CreateRegistry("Order", policy);
            var processor = new GdprProcessor(registry);
            var entry = BuildEntryWithProperties("Order", Prop("SSN", null, null));

            var (result, _, _) = processor.ApplyStoragePolicies(entry);

            Assert.IsNull(result.Properties.First().OldValue);
            Assert.IsNull(result.Properties.First().NewValue);
        }

        [TestMethod]
        public void ApplyStoragePolicies_AnonymizeAction_AnonymizesValues()
        {
            var policy = new EntityGdprPolicy
            {
                StorageRules = new[] { new FieldGdprRule { FieldName = "Name", Action = GdprFieldAction.Anonymize } }
            };
            var registry = CreateRegistry("Order", policy);
            var processor = new GdprProcessor(registry);
            var entry = BuildEntryWithProperties("Order", Prop("Name", "Alice", "Bob"));

            var (result, applied, _) = processor.ApplyStoragePolicies(entry);

            Assert.IsTrue(applied);
            Assert.AreEqual("[ANONYMIZED]", result.Properties.First().OldValue);
            Assert.AreEqual("[ANONYMIZED]", result.Properties.First().NewValue);
        }

        [TestMethod]
        public void ApplyStoragePolicies_AnonymizeAction_NullValue_ReturnsNull()
        {
            var policy = new EntityGdprPolicy
            {
                StorageRules = new[] { new FieldGdprRule { FieldName = "Name", Action = GdprFieldAction.Anonymize } }
            };
            var registry = CreateRegistry("Order", policy);
            var processor = new GdprProcessor(registry);
            var entry = BuildEntryWithProperties("Order", Prop("Name", null, "Bob"));

            var (result, _, _) = processor.ApplyStoragePolicies(entry);

            Assert.IsNull(result.Properties.First().OldValue);
            Assert.AreEqual("[ANONYMIZED]", result.Properties.First().NewValue);
        }

        [TestMethod]
        public void ApplyStoragePolicies_ExcludeAction_RemovesProperty()
        {
            var policy = new EntityGdprPolicy
            {
                StorageRules = new[] { new FieldGdprRule { FieldName = "CreditCard", Action = GdprFieldAction.Exclude } }
            };
            var registry = CreateRegistry("Order", policy);
            var processor = new GdprProcessor(registry);
            var entry = BuildEntryWithProperties("Order",
                Prop("Name", "Alice", "Bob"),
                Prop("CreditCard", "4111-1111", "4222-2222"));

            var (result, applied, _) = processor.ApplyStoragePolicies(entry);

            Assert.IsTrue(applied);
            Assert.AreEqual(1, result.Properties.Count);
            Assert.AreEqual("Name", result.Properties.First().PropertyName);
        }

        [TestMethod]
        public void ApplyStoragePolicies_ExcludeAction_MultipleExcluded_RemovesAll()
        {
            var policy = new EntityGdprPolicy
            {
                StorageRules = new[]
                {
                    new FieldGdprRule { FieldName = "A", Action = GdprFieldAction.Exclude },
                    new FieldGdprRule { FieldName = "B", Action = GdprFieldAction.Exclude }
                }
            };
            var registry = CreateRegistry("Order", policy);
            var processor = new GdprProcessor(registry);
            var entry = BuildEntryWithProperties("Order", Prop("A", "1", "2"), Prop("B", "3", "4"), Prop("C", "5", "6"));

            var (result, _, _) = processor.ApplyStoragePolicies(entry);

            Assert.AreEqual(1, result.Properties.Count);
            Assert.AreEqual("C", result.Properties.First().PropertyName);
        }

        [TestMethod]
        public void ApplyStoragePolicies_CustomAction_InvokesTransformer()
        {
            var policy = new EntityGdprPolicy
            {
                StorageRules = new[]
                {
                    new FieldGdprRule
                    {
                        FieldName = "Phone",
                        Action = GdprFieldAction.Custom,
                        CustomTransformer = v => "REDACTED"
                    }
                }
            };
            var registry = CreateRegistry("Order", policy);
            var processor = new GdprProcessor(registry);
            var entry = BuildEntryWithProperties("Order", Prop("Phone", "555-1234", "555-5678"));

            var (result, applied, _) = processor.ApplyStoragePolicies(entry);

            Assert.IsTrue(applied);
            Assert.AreEqual("REDACTED", result.Properties.First().OldValue);
            Assert.AreEqual("REDACTED", result.Properties.First().NewValue);
        }

        [TestMethod]
        public void ApplyStoragePolicies_CustomAction_NullValue_ReturnsNull()
        {
            var policy = new EntityGdprPolicy
            {
                StorageRules = new[]
                {
                    new FieldGdprRule
                    {
                        FieldName = "Phone",
                        Action = GdprFieldAction.Custom,
                        CustomTransformer = v => "NEVER"
                    }
                }
            };
            var registry = CreateRegistry("Order", policy);
            var processor = new GdprProcessor(registry);
            var entry = BuildEntryWithProperties("Order", Prop("Phone", null, "555"));

            var (result, _, _) = processor.ApplyStoragePolicies(entry);

            Assert.IsNull(result.Properties.First().OldValue);
            Assert.AreEqual("NEVER", result.Properties.First().NewValue);
        }

        [TestMethod]
        public void ApplyStoragePolicies_MixedRules_AppliesEachCorrectly()
        {
            var policy = new EntityGdprPolicy
            {
                StorageRules = new[]
                {
                    new FieldGdprRule { FieldName = "Email", Action = GdprFieldAction.Mask },
                    new FieldGdprRule { FieldName = "SSN", Action = GdprFieldAction.Hash },
                    new FieldGdprRule { FieldName = "CreditCard", Action = GdprFieldAction.Exclude }
                }
            };
            var registry = CreateRegistry("Order", policy);
            var processor = new GdprProcessor(registry);
            var entry = BuildEntryWithProperties("Order",
                Prop("Email", "test@mail.com", "new@mail.com"),
                Prop("SSN", "123-45-6789", null),
                Prop("CreditCard", "4111", "4222"),
                Prop("Name", "Alice", "Bob"));

            var (result, applied, _) = processor.ApplyStoragePolicies(entry);

            Assert.IsTrue(applied);
            Assert.AreEqual(3, result.Properties.Count); // CreditCard removed
            Assert.IsTrue(result.Properties.First(p => p.PropertyName == "Email").OldValue.Contains("*"));
            Assert.AreEqual(64, result.Properties.First(p => p.PropertyName == "SSN").OldValue.Length);
            Assert.AreEqual("Alice", result.Properties.First(p => p.PropertyName == "Name").OldValue);
        }

        [TestMethod]
        public void ApplyStoragePolicies_PropertyWithNoRule_LeftUnchanged()
        {
            var policy = new EntityGdprPolicy
            {
                StorageRules = new[] { new FieldGdprRule { FieldName = "Email", Action = GdprFieldAction.Mask } }
            };
            var registry = CreateRegistry("Order", policy);
            var processor = new GdprProcessor(registry);
            var entry = BuildEntryWithProperties("Order", Prop("Name", "Alice", "Bob"), Prop("Email", "test@x.com", "n@x.com"));

            var (result, _, _) = processor.ApplyStoragePolicies(entry);

            Assert.AreEqual("Alice", result.Properties.First(p => p.PropertyName == "Name").OldValue);
        }

        [TestMethod]
        public void ApplyRetrievalPolicies_WithNoPolicy_ReturnsOriginal()
        {
            var processor = new GdprProcessor(new GdprPolicyRegistry());
            var entry = BuildEntryWithProperties("Order", Prop("Email", "test@x.com", "new@x.com"));

            var result = processor.ApplyRetrievalPolicies(entry, new GdprRetrievalContext());

            Assert.AreEqual("test@x.com", result.Properties.First().OldValue);
        }

        [TestMethod]
        public void ApplyRetrievalPolicies_MaskAction_UnauthorizedUser_MasksValues()
        {
            var policy = new EntityGdprPolicy
            {
                RetrievalRules = new[]
                {
                    new FieldGdprRule { FieldName = "Email", Action = GdprFieldAction.Mask, AllowedRoles = new[] { "Admin" } }
                }
            };
            var registry = CreateRegistry("Order", policy);
            var processor = new GdprProcessor(registry);
            var entry = BuildEntryWithProperties("Order", Prop("Email", "user@test.com", "new@test.com"));

            var result = processor.ApplyRetrievalPolicies(entry, new GdprRetrievalContext());

            Assert.IsTrue(result.Properties.First().OldValue.Contains("*"));
        }

        [TestMethod]
        public void ApplyRetrievalPolicies_MaskAction_AuthorizedRole_ReturnsOriginal()
        {
            var policy = new EntityGdprPolicy
            {
                RetrievalRules = new[]
                {
                    new FieldGdprRule { FieldName = "Email", Action = GdprFieldAction.Mask, AllowedRoles = new[] { "Admin" } }
                }
            };
            var registry = CreateRegistry("Order", policy);
            var processor = new GdprProcessor(registry);
            var entry = BuildEntryWithProperties("Order", Prop("Email", "user@test.com", "new@test.com"));

            var ctx = new GdprRetrievalContext { UserRoles = new[] { "Admin" } };
            var result = processor.ApplyRetrievalPolicies(entry, ctx);

            Assert.AreEqual("user@test.com", result.Properties.First().OldValue);
        }

        [TestMethod]
        public void ApplyRetrievalPolicies_AnonymizeAction_UnauthorizedUser_Anonymizes()
        {
            var policy = new EntityGdprPolicy
            {
                RetrievalRules = new[]
                {
                    new FieldGdprRule { FieldName = "Phone", Action = GdprFieldAction.Anonymize, AllowedRoles = new[] { "Admin" } }
                }
            };
            var registry = CreateRegistry("Order", policy);
            var processor = new GdprProcessor(registry);
            var entry = BuildEntryWithProperties("Order", Prop("Phone", "555-1234", "555-5678"));

            var result = processor.ApplyRetrievalPolicies(entry, new GdprRetrievalContext());

            Assert.AreEqual("[ANONYMIZED]", result.Properties.First().OldValue);
            Assert.AreEqual("[ANONYMIZED]", result.Properties.First().NewValue);
        }

        [TestMethod]
        public void ApplyRetrievalPolicies_AuthorizedClaim_ReturnsOriginal()
        {
            var policy = new EntityGdprPolicy
            {
                RetrievalRules = new[]
                {
                    new FieldGdprRule
                    {
                        FieldName = "SSN",
                        Action = GdprFieldAction.Anonymize,
                        AllowedClaims = new Dictionary<string, string> { ["gdpr"] = "full" }
                    }
                }
            };
            var registry = CreateRegistry("Order", policy);
            var processor = new GdprProcessor(registry);
            var entry = BuildEntryWithProperties("Order", Prop("SSN", "123-45-6789", "987-65-4321"));

            var ctx = new GdprRetrievalContext { UserClaims = new Dictionary<string, string> { ["gdpr"] = "full" } };
            var result = processor.ApplyRetrievalPolicies(entry, ctx);

            Assert.AreEqual("123-45-6789", result.Properties.First().OldValue);
        }

        [TestMethod]
        public void ApplyRetrievalPolicies_WrongClaim_MasksValues()
        {
            var policy = new EntityGdprPolicy
            {
                RetrievalRules = new[]
                {
                    new FieldGdprRule
                    {
                        FieldName = "SSN",
                        Action = GdprFieldAction.Anonymize,
                        AllowedClaims = new Dictionary<string, string> { ["gdpr"] = "full" }
                    }
                }
            };
            var registry = CreateRegistry("Order", policy);
            var processor = new GdprProcessor(registry);
            var entry = BuildEntryWithProperties("Order", Prop("SSN", "123-45-6789", null));

            var ctx = new GdprRetrievalContext { UserClaims = new Dictionary<string, string> { ["gdpr"] = "partial" } };
            var result = processor.ApplyRetrievalPolicies(entry, ctx);

            Assert.AreEqual("[ANONYMIZED]", result.Properties.First().OldValue);
        }

        [TestMethod]
        public void ApplyRetrievalPolicies_EmptyRetrievalRules_ReturnsOriginal()
        {
            var policy = new EntityGdprPolicy { RetrievalRules = Array.Empty<FieldGdprRule>() };
            var registry = CreateRegistry("Order", policy);
            var processor = new GdprProcessor(registry);
            var entry = BuildEntryWithProperties("Order", Prop("Email", "test@x.com", "new@x.com"));

            var result = processor.ApplyRetrievalPolicies(entry, new GdprRetrievalContext());

            Assert.AreEqual("test@x.com", result.Properties.First().OldValue);
        }

        [TestMethod]
        public void ApplyStoragePolicies_AllAnonymize_ReturnsFullyAnonymized()
        {
            var policy = new EntityGdprPolicy
            {
                StorageRules = new[]
                {
                    new FieldGdprRule { FieldName = "Name", Action = GdprFieldAction.Anonymize },
                    new FieldGdprRule { FieldName = "Phone", Action = GdprFieldAction.Anonymize }
                }
            };
            var registry = CreateRegistry("Order", policy);
            var processor = new GdprProcessor(registry);
            var entry = BuildEntryWithProperties("Order", Prop("Name", "Alice", "Bob"), Prop("Phone", "555", "666"));

            var (_, applied, fullyAnonymized) = processor.ApplyStoragePolicies(entry);

            Assert.IsTrue(applied);
            Assert.IsTrue(fullyAnonymized);
        }

        [TestMethod]
        public void ApplyStoragePolicies_AllExclude_ReturnsFullyAnonymized()
        {
            var policy = new EntityGdprPolicy
            {
                StorageRules = new[]
                {
                    new FieldGdprRule { FieldName = "SSN", Action = GdprFieldAction.Exclude },
                    new FieldGdprRule { FieldName = "CreditCard", Action = GdprFieldAction.Exclude }
                }
            };
            var registry = CreateRegistry("Order", policy);
            var processor = new GdprProcessor(registry);
            var entry = BuildEntryWithProperties("Order",
                Prop("SSN", "123", null),
                Prop("CreditCard", "4111", "4222"),
                Prop("Name", "Alice", "Bob"));

            var (_, applied, fullyAnonymized) = processor.ApplyStoragePolicies(entry);

            Assert.IsTrue(applied);
            Assert.IsTrue(fullyAnonymized);
        }

        [TestMethod]
        public void ApplyStoragePolicies_MixAnonymizeAndExclude_ReturnsFullyAnonymized()
        {
            var policy = new EntityGdprPolicy
            {
                StorageRules = new[]
                {
                    new FieldGdprRule { FieldName = "Name", Action = GdprFieldAction.Anonymize },
                    new FieldGdprRule { FieldName = "CreditCard", Action = GdprFieldAction.Exclude }
                }
            };
            var registry = CreateRegistry("Order", policy);
            var processor = new GdprProcessor(registry);
            var entry = BuildEntryWithProperties("Order",
                Prop("Name", "Alice", "Bob"),
                Prop("CreditCard", "4111", "4222"));

            var (_, applied, fullyAnonymized) = processor.ApplyStoragePolicies(entry);

            Assert.IsTrue(applied);
            Assert.IsTrue(fullyAnonymized);
        }

        [TestMethod]
        public void ApplyStoragePolicies_MixMaskAndAnonymize_ReturnsNotFullyAnonymized()
        {
            var policy = new EntityGdprPolicy
            {
                StorageRules = new[]
                {
                    new FieldGdprRule { FieldName = "Email", Action = GdprFieldAction.Mask },
                    new FieldGdprRule { FieldName = "Name", Action = GdprFieldAction.Anonymize }
                }
            };
            var registry = CreateRegistry("Order", policy);
            var processor = new GdprProcessor(registry);
            var entry = BuildEntryWithProperties("Order",
                Prop("Email", "test@x.com", "n@x.com"),
                Prop("Name", "Alice", "Bob"));

            var (_, applied, fullyAnonymized) = processor.ApplyStoragePolicies(entry);

            Assert.IsTrue(applied);
            Assert.IsFalse(fullyAnonymized);
        }

        [TestMethod]
        public void ApplyStoragePolicies_HashOnly_ReturnsNotFullyAnonymized()
        {
            var policy = new EntityGdprPolicy
            {
                StorageRules = new[] { new FieldGdprRule { FieldName = "SSN", Action = GdprFieldAction.Hash } }
            };
            var registry = CreateRegistry("Order", policy);
            var processor = new GdprProcessor(registry);
            var entry = BuildEntryWithProperties("Order", Prop("SSN", "123-45-6789", null));

            var (_, applied, fullyAnonymized) = processor.ApplyStoragePolicies(entry);

            Assert.IsTrue(applied);
            Assert.IsFalse(fullyAnonymized);
        }

        [TestMethod]
        public void ApplyStoragePolicies_CustomOnly_ReturnsNotFullyAnonymized()
        {
            var policy = new EntityGdprPolicy
            {
                StorageRules = new[]
                {
                    new FieldGdprRule
                    {
                        FieldName = "Notes",
                        Action = GdprFieldAction.Custom,
                        CustomTransformer = v => "REDACTED"
                    }
                }
            };
            var registry = CreateRegistry("Order", policy);
            var processor = new GdprProcessor(registry);
            var entry = BuildEntryWithProperties("Order", Prop("Notes", "secret", "classified"));

            var (_, applied, fullyAnonymized) = processor.ApplyStoragePolicies(entry);

            Assert.IsTrue(applied);
            Assert.IsFalse(fullyAnonymized);
        }

        [TestMethod]
        public void ApplyStoragePolicies_NoPolicy_FullyAnonymizedIsFalse()
        {
            var processor = new GdprProcessor(new GdprPolicyRegistry());
            var entry = BuildEntryWithProperties("Order", Prop("Name", "Alice", "Bob"));

            var (_, applied, fullyAnonymized) = processor.ApplyStoragePolicies(entry);

            Assert.IsFalse(applied);
            Assert.IsFalse(fullyAnonymized);
        }
    }
}
