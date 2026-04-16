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
    /// <summary>
    ///     Integration tests: verify that users are allowed (or denied) access to audit log
    ///     fields based on GDPR retrieval policies configured with roles and/or claims.
    /// </summary>
    [TestClass]
    public class GdprRetrievalAccessTests
    {
        private GdprProcessor _processor;
        private ServiceProvider _sp;

        /// <summary>
        ///     Configures GDPR retrieval policies via the fluent API and resolves the
        ///     processor from DI so every test exercises the full registration pipeline.
        ///     <para>
        ///         Email  - MaskOnRetrieval, AllowRoles("Admin", "Auditor")
        ///         Ssn    - AnonymizeOnRetrieval, AllowClaim("gdpr", "full")
        ///         Phone  - MaskOnRetrieval, AllowRoles("Admin") + AllowClaim("support", "tier2")
        ///     </para>
        /// </summary>
        [TestInitialize]
        public void Setup()
        {
            var services = new ServiceCollection();

            services.AddAuditTrail(opts =>
            {
                opts.Gdpr.ForEntity<CustomerEntity>(e =>
                {
                    e.MaskOnRetrieval(c => c.Email, access => access
                        .AllowRoles("Admin", "Auditor"));

                    e.AnonymizeOnRetrieval(c => c.Ssn, access => access
                        .AllowClaim("gdpr", "full"));

                    e.MaskOnRetrieval(c => c.Phone, access => access
                        .AllowRoles("Admin")
                        .AllowClaim("support", "tier2"));
                });
            });

            _sp = services.BuildServiceProvider();
            _processor = _sp.GetRequiredService<GdprProcessor>();
        }

        [TestCleanup]
        public void Cleanup() => _sp?.Dispose();

        [TestMethod]
        public void AdminRole_SeesUnmaskedEmail()
        {
            var entry = BuildEntryWithProperties("CustomerEntity",
                Prop("Email", "admin@test.com", "new@test.com"));

            var ctx = new GdprRetrievalContext { UserRoles = new[] { "Admin" } };
            var result = _processor.ApplyRetrievalPolicies(entry, ctx);

            Assert.AreEqual("admin@test.com", result.Properties.First().OldValue);
            Assert.AreEqual("new@test.com", result.Properties.First().NewValue);
        }

        [TestMethod]
        public void AuditorRole_SeesUnmaskedEmail()
        {
            var entry = BuildEntryWithProperties("CustomerEntity",
                Prop("Email", "user@test.com", "changed@test.com"));

            var ctx = new GdprRetrievalContext { UserRoles = new[] { "Auditor" } };
            var result = _processor.ApplyRetrievalPolicies(entry, ctx);

            Assert.AreEqual("user@test.com", result.Properties.First().OldValue);
            Assert.AreEqual("changed@test.com", result.Properties.First().NewValue);
        }

        [TestMethod]
        public void RegularUserRole_SeesMaskedEmail()
        {
            var entry = BuildEntryWithProperties("CustomerEntity",
                Prop("Email", "user@test.com", "changed@test.com"));

            var ctx = new GdprRetrievalContext { UserRoles = new[] { "User" } };
            var result = _processor.ApplyRetrievalPolicies(entry, ctx);

            Assert.AreEqual("u***********m", result.Properties.First().OldValue);
            Assert.AreEqual("c**************m", result.Properties.First().NewValue);
        }

        [TestMethod]
        public void NoRoles_SeesMaskedEmail()
        {
            var entry = BuildEntryWithProperties("CustomerEntity",
                Prop("Email", "user@test.com", "changed@test.com"));

            var result = _processor.ApplyRetrievalPolicies(entry, new GdprRetrievalContext());

            Assert.AreEqual("u***********m", result.Properties.First().OldValue);
            Assert.AreEqual("c**************m", result.Properties.First().NewValue);
        }
        
        [TestMethod]
        public void CorrectClaim_SeesUnmaskedSsn()
        {
            var entry = BuildEntryWithProperties("CustomerEntity",
                Prop("Ssn", "123-45-6789", "987-65-4321"));

            var ctx = new GdprRetrievalContext
            {
                UserClaims = new Dictionary<string, string> { ["gdpr"] = "full" }
            };
            var result = _processor.ApplyRetrievalPolicies(entry, ctx);

            Assert.AreEqual("123-45-6789", result.Properties.First().OldValue);
            Assert.AreEqual("987-65-4321", result.Properties.First().NewValue);
        }

        [TestMethod]
        public void WrongClaimValue_SeesAnonymizedSsn()
        {
            var entry = BuildEntryWithProperties("CustomerEntity",
                Prop("Ssn", "123-45-6789", "987-65-4321"));

            var ctx = new GdprRetrievalContext
            {
                UserClaims = new Dictionary<string, string> { ["gdpr"] = "partial" }
            };
            var result = _processor.ApplyRetrievalPolicies(entry, ctx);

            Assert.AreEqual("[ANONYMIZED]", result.Properties.First().OldValue);
            Assert.AreEqual("[ANONYMIZED]", result.Properties.First().NewValue);
        }

        [TestMethod]
        public void NoClaims_SeesAnonymizedSsn()
        {
            var entry = BuildEntryWithProperties("CustomerEntity",
                Prop("Ssn", "123-45-6789", null));

            var result = _processor.ApplyRetrievalPolicies(entry, new GdprRetrievalContext());

            Assert.AreEqual("[ANONYMIZED]", result.Properties.First().OldValue);
            // AsAnonymizedIfPresent returns null for null input
            Assert.IsNull(result.Properties.First().NewValue);
        }

        [TestMethod]
        public void RoleFailsButClaimPasses_SeesUnmaskedPhone()
        {
            var entry = BuildEntryWithProperties("CustomerEntity",
                Prop("Phone", "555-1234", "555-5678"));

            var ctx = new GdprRetrievalContext
            {
                UserRoles = new[] { "User" },
                UserClaims = new Dictionary<string, string> { ["support"] = "tier2" }
            };
            var result = _processor.ApplyRetrievalPolicies(entry, ctx);

            Assert.AreEqual("555-1234", result.Properties.First().OldValue);
            Assert.AreEqual("555-5678", result.Properties.First().NewValue);
        }

        [TestMethod]
        public void RolePassesButNoClaim_SeesUnmaskedPhone()
        {
            var entry = BuildEntryWithProperties("CustomerEntity",
                Prop("Phone", "555-1234", "555-5678"));

            var ctx = new GdprRetrievalContext { UserRoles = new[] { "Admin" } };
            var result = _processor.ApplyRetrievalPolicies(entry, ctx);

            Assert.AreEqual("555-1234", result.Properties.First().OldValue);
            Assert.AreEqual("555-5678", result.Properties.First().NewValue);
        }

        [TestMethod]
        public void NeitherRoleNorClaim_SeesMaskedPhone()
        {
            var entry = BuildEntryWithProperties("CustomerEntity",
                Prop("Phone", "555-1234", "555-5678"));

            var ctx = new GdprRetrievalContext { UserRoles = new[] { "User" } };
            var result = _processor.ApplyRetrievalPolicies(entry, ctx);

            Assert.AreEqual("5******4", result.Properties.First().OldValue);
            Assert.AreEqual("5******8", result.Properties.First().NewValue);
        }

        [TestMethod]
        public void AdminRole_SeesEmailAndPhone_ButNotSsn()
        {
            var entry = BuildEntryWithProperties("CustomerEntity",
                Prop("Email", "admin@test.com", "new@test.com"),
                Prop("Ssn", "123-45-6789", "987-65-4321"),
                Prop("Phone", "555-1234", "555-5678"));

            var ctx = new GdprRetrievalContext { UserRoles = new[] { "Admin" } };
            var result = _processor.ApplyRetrievalPolicies(entry, ctx);

            var props = result.Properties.ToList();

            // Email — Admin is allowed
            Assert.AreEqual("admin@test.com", props[0].OldValue);

            // SSN — requires claim "gdpr=full", Admin role has no effect
            Assert.AreEqual("[ANONYMIZED]", props[1].OldValue);

            // Phone — Admin role is allowed
            Assert.AreEqual("555-1234", props[2].OldValue);
        }

        [TestMethod]
        public void FullAccess_AllFieldsVisible()
        {
            var entry = BuildEntryWithProperties("CustomerEntity",
                Prop("Email", "admin@test.com", "new@test.com"),
                Prop("Ssn", "123-45-6789", "987-65-4321"),
                Prop("Phone", "555-1234", "555-5678"));

            var ctx = new GdprRetrievalContext
            {
                UserRoles = new[] { "Admin" },
                UserClaims = new Dictionary<string, string>
                {
                    ["gdpr"] = "full",
                    ["support"] = "tier2"
                }
            };
            var result = _processor.ApplyRetrievalPolicies(entry, ctx);

            var props = result.Properties.ToList();
            Assert.AreEqual("admin@test.com", props[0].OldValue);
            Assert.AreEqual("123-45-6789", props[1].OldValue);
            Assert.AreEqual("555-1234", props[2].OldValue);
        }

        [TestMethod]
        public void NoAccess_AllFieldsProtected()
        {
            var entry = BuildEntryWithProperties("CustomerEntity",
                Prop("Email", "admin@test.com", "new@test.com"),
                Prop("Ssn", "123-45-6789", "987-65-4321"),
                Prop("Phone", "555-1234", "555-5678"));

            var result = _processor.ApplyRetrievalPolicies(entry, new GdprRetrievalContext());

            var props = result.Properties.ToList();
            Assert.AreEqual("a************m", props[0].OldValue);    // masked
            Assert.AreEqual("[ANONYMIZED]", props[1].OldValue);       // anonymized
            Assert.AreEqual("5******4", props[2].OldValue);            // masked
        }

        [TestMethod]
        public void UnprotectedField_AlwaysVisible()
        {
            var entry = BuildEntryWithProperties("CustomerEntity",
                Prop("Name", "Alice", "Bob"));

            var result = _processor.ApplyRetrievalPolicies(entry, new GdprRetrievalContext());

            Assert.AreEqual("Alice", result.Properties.First().OldValue);
            Assert.AreEqual("Bob", result.Properties.First().NewValue);
        }

        [TestMethod]
        public void UnregisteredEntity_AllFieldsVisible()
        {
            var entry = BuildEntryWithProperties("UnknownEntity",
                Prop("Secret", "TopSecret", "Classified"));

            var result = _processor.ApplyRetrievalPolicies(entry, new GdprRetrievalContext());

            Assert.AreEqual("TopSecret", result.Properties.First().OldValue);
            Assert.AreEqual("Classified", result.Properties.First().NewValue);
        }

        [TestMethod]
        public void NullOldValue_StaysNull_WhenMasked()
        {
            var entry = BuildEntryWithProperties("CustomerEntity",
                Prop("Email", null, "new@test.com"));

            var result = _processor.ApplyRetrievalPolicies(entry, new GdprRetrievalContext());

            // MaskValue returns null for null input
            Assert.IsNull(result.Properties.First().OldValue);
            Assert.AreEqual("n**********m", result.Properties.First().NewValue);
        }

        [TestMethod]
        public void ShortValue_MaskedAsStars()
        {
            var entry = BuildEntryWithProperties("CustomerEntity",
                Prop("Email", "AB", "X"));

            var result = _processor.ApplyRetrievalPolicies(entry, new GdprRetrievalContext());

            Assert.AreEqual("***", result.Properties.First().OldValue);
            Assert.AreEqual("***", result.Properties.First().NewValue);
        }
    }
}
