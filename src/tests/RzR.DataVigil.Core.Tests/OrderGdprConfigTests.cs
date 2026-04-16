using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RzR.DataVigil.Abstractions.Models.Gdpr;
using RzR.DataVigil.Core.Extensions;
using RzR.DataVigil.Core.Gdpr;
using RzR.DataVigil.Core.Tests.Models;
using static RzR.DataVigil.Core.Tests.Helpers.AuditTestDataBuilder;

namespace RzR.DataVigil.Core.Tests
{
    [TestClass]
    public class OrderGdprConfigTests
    {
        private GdprProcessor _processor;
        private ServiceProvider _sp;

        [TestInitialize]
        public void Setup()
        {
            var services = new ServiceCollection();

            services.AddAuditTrail(opts =>
            {
                opts.Gdpr.ForEntity<Order>(e =>
                {
                    e.MaskOnStorage(o => o.CustomerEmail);
                    e.MaskOnStorage(o => o.CustomerPhone);

                    e.MaskOnRetrieval(o => o.CustomerEmail, a => a
                        .AllowRoles("Admin"));

                    e.AnonymizeOnRetrieval(o => o.CustomerPhone, a => a
                        .AllowClaim("gdpr", "full"));
                });
            });

            _sp = services.BuildServiceProvider();
            _processor = _sp.GetRequiredService<GdprProcessor>();
        }

        [TestCleanup]
        public void Cleanup() => _sp?.Dispose();

        [TestMethod]
        public void Storage_CustomerEmail_IsMasked()
        {
            var entry = BuildEntryWithProperties("Order",
                Prop("CustomerEmail", "alice@contoso.com", "bob@contoso.com"));

            var (result, applied, _) = _processor.ApplyStoragePolicies(entry);

            Assert.IsTrue(applied);

            var prop = result.Properties.First();
            Assert.IsTrue(prop.OldValue.Contains("*"), "Old email should be masked");
            Assert.IsTrue(prop.NewValue.Contains("*"), "New email should be masked");
            Assert.AreNotEqual("alice@contoso.com", prop.OldValue);
            Assert.AreNotEqual("bob@contoso.com", prop.NewValue);
        }

        [TestMethod]
        public void Storage_CustomerPhone_IsMasked()
        {
            var entry = BuildEntryWithProperties("Order",
                Prop("CustomerPhone", "+1-555-123-4567", "+1-555-987-6543"));

            var (result, applied, _) = _processor.ApplyStoragePolicies(entry);

            Assert.IsTrue(applied);

            var prop = result.Properties.First();
            Assert.IsTrue(prop.OldValue.Contains("*"), "Old phone should be masked");
            Assert.IsTrue(prop.NewValue.Contains("*"), "New phone should be masked");
            Assert.AreNotEqual("+1-555-123-4567", prop.OldValue);
        }

        [TestMethod]
        public void Storage_BothFields_MaskedSimultaneously()
        {
            var entry = BuildEntryWithProperties("Order",
                Prop("CustomerEmail", "alice@contoso.com", "bob@contoso.com"),
                Prop("CustomerPhone", "555-1234", "555-5678"));

            var (result, applied, _) = _processor.ApplyStoragePolicies(entry);

            Assert.IsTrue(applied);
            Assert.AreEqual(2, result.Properties.Count);

            var props = result.Properties.ToList();
            Assert.IsTrue(props[0].OldValue.Contains("*"));
            Assert.IsTrue(props[1].OldValue.Contains("*"));
        }

        [TestMethod]
        public void Storage_MaskPreservesFirstAndLastChar()
        {
            var entry = BuildEntryWithProperties("Order",
                Prop("CustomerEmail", "alice@contoso.com", null));

            var (result, _, _) = _processor.ApplyStoragePolicies(entry);

            var masked = result.Properties.First().OldValue;
            Assert.AreEqual('a', masked[0], "First char should be preserved");
            Assert.AreEqual('m', masked[masked.Length - 1], "Last char should be preserved");
        }

        [TestMethod]
        public void Storage_NullEmail_StaysNull()
        {
            var entry = BuildEntryWithProperties("Order",
                Prop("CustomerEmail", null, "new@contoso.com"));

            var (result, _, _) = _processor.ApplyStoragePolicies(entry);

            Assert.IsNull(result.Properties.First().OldValue);
            Assert.IsTrue(result.Properties.First().NewValue.Contains("*"));
        }

        [TestMethod]
        public void Storage_NullPhone_StaysNull()
        {
            var entry = BuildEntryWithProperties("Order",
                Prop("CustomerPhone", null, null));

            var (result, _, _) = _processor.ApplyStoragePolicies(entry);

            Assert.IsNull(result.Properties.First().OldValue);
            Assert.IsNull(result.Properties.First().NewValue);
        }

        [TestMethod]
        public void Storage_UnprotectedField_NotMasked()
        {
            var entry = BuildEntryWithProperties("Order",
                Prop("TotalAmount", "99.95", "149.95"));

            var (result, applied, _) = _processor.ApplyStoragePolicies(entry);

            Assert.IsFalse(applied, "TotalAmount has no storage rule");
            Assert.AreEqual("99.95", result.Properties.First().OldValue);
            Assert.AreEqual("149.95", result.Properties.First().NewValue);
        }

        [TestMethod]
        public void Storage_MixedProtectedAndUnprotected()
        {
            var entry = BuildEntryWithProperties("Order",
                Prop("CustomerEmail", "alice@contoso.com", "bob@contoso.com"),
                Prop("TotalAmount", "99.95", "149.95"),
                Prop("CustomerPhone", "555-1234", "555-5678"));

            var (result, applied, _) = _processor.ApplyStoragePolicies(entry);

            Assert.IsTrue(applied);
            var props = result.Properties.ToList();

            Assert.IsTrue(props[0].OldValue.Contains("*"));
            Assert.AreEqual("99.95", props[1].OldValue);
            Assert.IsTrue(props[2].OldValue.Contains("*"));
        }

        [TestMethod]
        public void Retrieval_AdminRole_SeesUnmaskedEmail()
        {
            var entry = BuildEntryWithProperties("Order",
                Prop("CustomerEmail", "alice@contoso.com", "bob@contoso.com"));

            var ctx = new GdprRetrievalContext { UserRoles = new[] { "Admin" } };
            var result = _processor.ApplyRetrievalPolicies(entry, ctx);

            Assert.AreEqual("alice@contoso.com", result.Properties.First().OldValue);
            Assert.AreEqual("bob@contoso.com", result.Properties.First().NewValue);
        }

        [TestMethod]
        public void Retrieval_NonAdminRole_SeesMaskedEmail()
        {
            var entry = BuildEntryWithProperties("Order",
                Prop("CustomerEmail", "alice@contoso.com", "bob@contoso.com"));

            var ctx = new GdprRetrievalContext { UserRoles = new[] { "User" } };
            var result = _processor.ApplyRetrievalPolicies(entry, ctx);

            Assert.IsTrue(result.Properties.First().OldValue.Contains("*"));
            Assert.AreNotEqual("alice@contoso.com", result.Properties.First().OldValue);
        }

        [TestMethod]
        public void Retrieval_NoContext_SeesMaskedEmail()
        {
            var entry = BuildEntryWithProperties("Order",
                Prop("CustomerEmail", "alice@contoso.com", "bob@contoso.com"));

            var result = _processor.ApplyRetrievalPolicies(entry, new GdprRetrievalContext());

            Assert.IsTrue(result.Properties.First().OldValue.Contains("*"));
            Assert.IsTrue(result.Properties.First().NewValue.Contains("*"));
        }

        [TestMethod]
        public void Retrieval_GdprClaimAlone_DoesNotUnlockEmail()
        {
            var entry = BuildEntryWithProperties("Order",
                Prop("CustomerEmail", "alice@contoso.com", "bob@contoso.com"));

            var ctx = new GdprRetrievalContext
            {
                UserClaims = new Dictionary<string, string> { ["gdpr"] = "full" }
            };
            var result = _processor.ApplyRetrievalPolicies(entry, ctx);

            Assert.IsTrue(result.Properties.First().OldValue.Contains("*"), "gdpr=full claim should not unlock email — only Admin role can");
        }

        [TestMethod]
        public void Retrieval_GdprFullClaim_SeesUnmaskedPhone()
        {
            var entry = BuildEntryWithProperties("Order",
                Prop("CustomerPhone", "555-1234", "555-5678"));

            var ctx = new GdprRetrievalContext
            {
                UserClaims = new Dictionary<string, string> { ["gdpr"] = "full" }
            };
            var result = _processor.ApplyRetrievalPolicies(entry, ctx);

            Assert.AreEqual("555-1234", result.Properties.First().OldValue);
            Assert.AreEqual("555-5678", result.Properties.First().NewValue);
        }

        [TestMethod]
        public void Retrieval_WrongClaimValue_SeesAnonymizedPhone()
        {
            var entry = BuildEntryWithProperties("Order",
                Prop("CustomerPhone", "555-1234", "555-5678"));

            var ctx = new GdprRetrievalContext
            {
                UserClaims = new Dictionary<string, string> { ["gdpr"] = "partial" }
            };
            var result = _processor.ApplyRetrievalPolicies(entry, ctx);

            Assert.AreEqual("[ANONYMIZED]", result.Properties.First().OldValue);
            Assert.AreEqual("[ANONYMIZED]", result.Properties.First().NewValue);
        }

        [TestMethod]
        public void Retrieval_NoClaims_SeesAnonymizedPhone()
        {
            var entry = BuildEntryWithProperties("Order",
                Prop("CustomerPhone", "555-1234", "555-5678"));

            var result = _processor.ApplyRetrievalPolicies(entry, new GdprRetrievalContext());

            Assert.AreEqual("[ANONYMIZED]", result.Properties.First().OldValue);
            Assert.AreEqual("[ANONYMIZED]", result.Properties.First().NewValue);
        }

        [TestMethod]
        public void Retrieval_AdminRoleAlone_DoesNotUnlockPhone()
        {
            // Phone is claim-gated ("gdpr=full"), not role-gated
            var entry = BuildEntryWithProperties("Order",
                Prop("CustomerPhone", "555-1234", "555-5678"));

            var ctx = new GdprRetrievalContext { UserRoles = new[] { "Admin" } };
            var result = _processor.ApplyRetrievalPolicies(entry, ctx);

            Assert.AreEqual("[ANONYMIZED]", result.Properties.First().OldValue, "Admin role should not unlock phone — only gdpr=full claim can");
        }

        [TestMethod]
        public void Retrieval_NullPhone_StaysNullWhenAnonymized()
        {
            var entry = BuildEntryWithProperties("Order",
                Prop("CustomerPhone", null, "555-5678"));

            var result = _processor.ApplyRetrievalPolicies(entry, new GdprRetrievalContext());

            Assert.IsNull(result.Properties.First().OldValue, "Null input should remain null even when anonymized");
            Assert.AreEqual("[ANONYMIZED]", result.Properties.First().NewValue);
        }

        [TestMethod]
        public void Retrieval_AdminWithGdprFull_SeesAllFields()
        {
            var entry = BuildEntryWithProperties("Order",
                Prop("CustomerEmail", "alice@contoso.com", "bob@contoso.com"),
                Prop("CustomerPhone", "555-1234", "555-5678"),
                Prop("TotalAmount", "99.95", "149.95"));

            var ctx = new GdprRetrievalContext
            {
                UserRoles = new[] { "Admin" },
                UserClaims = new Dictionary<string, string> { ["gdpr"] = "full" }
            };
            var result = _processor.ApplyRetrievalPolicies(entry, ctx);

            var props = result.Properties.ToList();
            Assert.AreEqual("alice@contoso.com", props[0].OldValue);
            Assert.AreEqual("555-1234", props[1].OldValue);
            Assert.AreEqual("99.95", props[2].OldValue);
        }

        [TestMethod]
        public void Retrieval_NoAccess_EmailMaskedPhoneAnonymized()
        {
            var entry = BuildEntryWithProperties("Order",
                Prop("CustomerEmail", "alice@contoso.com", "bob@contoso.com"),
                Prop("CustomerPhone", "555-1234", "555-5678"));

            var result = _processor.ApplyRetrievalPolicies(entry, new GdprRetrievalContext());

            var props = result.Properties.ToList();

            Assert.IsTrue(props[0].OldValue.Contains("*"));
            Assert.AreEqual('a', props[0].OldValue[0]);
            Assert.AreEqual("[ANONYMIZED]", props[1].OldValue);
        }

        [TestMethod]
        public void Retrieval_AdminOnly_EmailVisiblePhoneAnonymized()
        {
            var entry = BuildEntryWithProperties("Order",
                Prop("CustomerEmail", "alice@contoso.com", "bob@contoso.com"),
                Prop("CustomerPhone", "555-1234", "555-5678"));

            var ctx = new GdprRetrievalContext { UserRoles = new[] { "Admin" } };
            var result = _processor.ApplyRetrievalPolicies(entry, ctx);

            var props = result.Properties.ToList();
            Assert.AreEqual("alice@contoso.com", props[0].OldValue, "Admin should see email");
            Assert.AreEqual("[ANONYMIZED]", props[1].OldValue, "Admin without gdpr=full should not see phone");
        }

        [TestMethod]
        public void Retrieval_GdprFullOnly_PhoneVisibleEmailMasked()
        {
            var entry = BuildEntryWithProperties("Order",
                Prop("CustomerEmail", "alice@contoso.com", "bob@contoso.com"),
                Prop("CustomerPhone", "555-1234", "555-5678"));

            var ctx = new GdprRetrievalContext
            {
                UserClaims = new Dictionary<string, string> { ["gdpr"] = "full" }
            };
            var result = _processor.ApplyRetrievalPolicies(entry, ctx);

            var props = result.Properties.ToList();
            Assert.IsTrue(props[0].OldValue.Contains("*"), "gdpr=full alone should not unlock email");
            Assert.AreEqual("555-1234", props[1].OldValue, "gdpr=full should unlock phone");
        }

        [TestMethod]
        public void Retrieval_UnprotectedField_AlwaysVisible()
        {
            var entry = BuildEntryWithProperties("Order",
                Prop("ShippingAddress", "123 Main St", "456 Oak Ave"));

            var result = _processor.ApplyRetrievalPolicies(entry, new GdprRetrievalContext());

            Assert.AreEqual("123 Main St", result.Properties.First().OldValue);
            Assert.AreEqual("456 Oak Ave", result.Properties.First().NewValue);
        }

        [TestMethod]
        public void Retrieval_IrrelevantRole_BothFieldsProtected()
        {
            var entry = BuildEntryWithProperties("Order",
                Prop("CustomerEmail", "alice@contoso.com", "bob@contoso.com"),
                Prop("CustomerPhone", "555-1234", "555-5678"));

            var ctx = new GdprRetrievalContext { UserRoles = new[] { "Viewer" } };
            var result = _processor.ApplyRetrievalPolicies(entry, ctx);

            var props = result.Properties.ToList();
            Assert.IsTrue(props[0].OldValue.Contains("*"), "Viewer sees masked email");
            Assert.AreEqual("[ANONYMIZED]", props[1].OldValue, "Viewer sees anonymized phone");
        }

        [TestMethod]
        public void Retrieval_IrrelevantClaim_BothFieldsProtected()
        {
            var entry = BuildEntryWithProperties("Order",
                Prop("CustomerEmail", "alice@contoso.com", "bob@contoso.com"),
                Prop("CustomerPhone", "555-1234", "555-5678"));

            var ctx = new GdprRetrievalContext
            {
                UserClaims = new Dictionary<string, string> { ["department"] = "sales" }
            };
            var result = _processor.ApplyRetrievalPolicies(entry, ctx);

            var props = result.Properties.ToList();
            Assert.IsTrue(props[0].OldValue.Contains("*"));
            Assert.AreEqual("[ANONYMIZED]", props[1].OldValue);
        }

        [TestMethod]
        public void Pipeline_StorageThenRetrieval_DoubleProtection()
        {
            // First: storage masks both fields
            var entry = BuildEntryWithProperties("Order",
                Prop("CustomerEmail", "alice@contoso.com", "bob@contoso.com"),
                Prop("CustomerPhone", "555-1234", "555-5678"));

            var (storedEntry, storageApplied, _) = _processor.ApplyStoragePolicies(entry);
            Assert.IsTrue(storageApplied);

            // Verify stored values are already masked
            var storedProps = storedEntry.Properties.ToList();
            var storedEmail = storedProps[0].OldValue;
            var storedPhone = storedProps[1].OldValue;
            Assert.IsTrue(storedEmail.Contains("*"));
            Assert.IsTrue(storedPhone.Contains("*"));

            // Now: retrieval by a user with no access applies retrieval rules on top of stored data
            var retrieved = _processor.ApplyRetrievalPolicies(storedEntry, new GdprRetrievalContext());

            var props = retrieved.Properties.ToList();
            Assert.IsTrue(props[0].OldValue.Contains("*"));
            Assert.AreEqual("[ANONYMIZED]", props[1].OldValue);
        }

        [TestMethod]
        public void Pipeline_StorageThenRetrievalByAdmin_EmailRecoveredPhoneAnonymized()
        {
            // Storage masks both fields
            var entry = BuildEntryWithProperties("Order",
                Prop("CustomerEmail", "alice@contoso.com", "bob@contoso.com"),
                Prop("CustomerPhone", "555-1234", "555-5678"));

            var (storedEntry, _, _) = _processor.ApplyStoragePolicies(entry);

            // Admin retrieval: email rule skips masking (Admin allowed), phone anonymized
            var ctx = new GdprRetrievalContext { UserRoles = new[] { "Admin" } };
            var retrieved = _processor.ApplyRetrievalPolicies(storedEntry, ctx);

            var props = retrieved.Properties.ToList();

            // Email: Admin bypasses retrieval mask, but sees storage-masked value
            Assert.IsTrue(props[0].OldValue.Contains("*"), "Storage mask persists — Admin skips retrieval mask but sees stored value");

            // Phone: Admin has no gdpr=full claim, so retrieval anonymizes
            Assert.AreEqual("[ANONYMIZED]", props[1].OldValue);
        }

        [TestMethod]
        public void Pipeline_StorageThenRetrievalByGdprFull_PhoneNotAnonymizedEmailReMasked()
        {
            var entry = BuildEntryWithProperties("Order",
                Prop("CustomerEmail", "alice@contoso.com", "bob@contoso.com"),
                Prop("CustomerPhone", "555-1234", "555-5678"));

            var (storedEntry, _, _) = _processor.ApplyStoragePolicies(entry);

            var ctx = new GdprRetrievalContext
            {
                UserClaims = new Dictionary<string, string> { ["gdpr"] = "full" }
            };
            var retrieved = _processor.ApplyRetrievalPolicies(storedEntry, ctx);

            var props = retrieved.Properties.ToList();

            // Email: no Admin role - retrieval re-masks stored value
            Assert.IsTrue(props[0].OldValue.Contains("*"));

            // Phone: gdpr=full bypasses retrieval anonymize - sees storage-masked value
            Assert.IsTrue(props[1].OldValue.Contains("*"), "gdpr=full bypasses retrieval, sees storage-masked phone");
            Assert.AreNotEqual("[ANONYMIZED]", props[1].OldValue);
        }
    }
}
