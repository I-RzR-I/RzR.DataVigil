using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RzR.DataVigil.Abstractions.Constants;
using RzR.DataVigil.Abstractions.Enums;
using RzR.DataVigil.Abstractions.Models.Entries;
using RzR.DataVigil.Abstractions.Models.Gdpr;
using RzR.DataVigil.Abstractions.Models.Identity;
using RzR.DataVigil.Core.Gdpr;
using RzR.DataVigil.Core.Pipeline;
using RzR.DataVigil.Core.Tests.Resolvers;
using RzR.DataVigil.Core.Tests.Stubs;
using static RzR.DataVigil.Core.Tests.Helpers.AuditTestDataBuilder;
using static RzR.DataVigil.Core.Tests.Helpers.GdprPolicyRegistryHelper;

namespace RzR.DataVigil.Core.Tests
{
    [TestClass]
    public class AuditPipelineTests
    {
        private StubUserResolver _userResolver;
        private StubSourceResolver _sourceResolver;
        private StubCorrelationProvider _correlationProvider;
        private StubAuditStore _store;
        private GdprProcessor _gdprProcessor;
        private AuditPipeline _pipeline;

        [TestInitialize]
        public void Init()
        {
            _userResolver = new StubUserResolver
            {
                UserToReturn = new AuditUserInfo
                {
                    UserId = "user-1",
                    UserName = "Alice",
                    IpAddress = "10.0.0.1"
                }
            };
            _sourceResolver = new StubSourceResolver { SourceToReturn = "WebApi" };
            _correlationProvider = new StubCorrelationProvider { CorrelationId = "c-1", TraceId = "t-1" };
            _store = new StubAuditStore();
            _gdprProcessor = new GdprProcessor(new GdprPolicyRegistry());
            _pipeline = new AuditPipeline(_userResolver, _sourceResolver, _correlationProvider, _gdprProcessor, _store);
        }

        [TestMethod]
        public async Task ProcessAsync_NullTransaction_ReturnsSuccess()
        {
            var result = await _pipeline.ProcessAsync(null);
            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(0, _store.SaveCallCount);
        }

        [TestMethod]
        public async Task ProcessAsync_EmptyEntries_ReturnsSuccess()
        {
            var tx = new AuditTransaction
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTimeOffset.UtcNow,
                Entries = new List<AuditEntry>()
            };

            var result = await _pipeline.ProcessAsync(tx);
            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(0, _store.SaveCallCount);
        }

        [TestMethod]
        public async Task ProcessAsync_EnrichesTransactionWithUserInfo()
        {
            var tx = BuildTransaction(BuildEntry());

            await _pipeline.ProcessAsync(tx);

            Assert.AreEqual("user-1", tx.UserId);
            Assert.AreEqual("Alice", tx.UserName);
            Assert.AreEqual("10.0.0.1", tx.IpAddress);
        }

        [TestMethod]
        public async Task ProcessAsync_EnrichesTransactionWithSourceAndCorrelation()
        {
            var tx = BuildTransaction(BuildEntry());

            await _pipeline.ProcessAsync(tx);

            Assert.AreEqual("WebApi", tx.Source);
            Assert.AreEqual("c-1", tx.CorrelationId);
            Assert.AreEqual("t-1", tx.TraceId);
        }

        [TestMethod]
        public async Task ProcessAsync_NullUser_DoesNotSetUserId()
        {
            _userResolver.UserToReturn = null;
            var tx = BuildTransaction(BuildEntry());

            await _pipeline.ProcessAsync(tx);

            Assert.IsNull(tx.UserId);
        }

        [TestMethod]
        public async Task ProcessAsync_CallsSaveAsync()
        {
            var tx = BuildTransaction(BuildEntry());

            var result = await _pipeline.ProcessAsync(tx);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(1, _store.SaveCallCount);
            Assert.AreSame(tx, _store.LastSaved);
        }

        [TestMethod]
        public async Task ProcessAsync_NoGdprPolicy_GdprStateRemainsDefault()
        {
            var tx = BuildTransaction(BuildEntry());

            await _pipeline.ProcessAsync(tx);

            Assert.AreEqual(GdprStorageState.Original, tx.GdprState);
        }
        
        [TestMethod]
        public async Task ProcessAsync_WithGdprPolicy_SetsPartiallyProcessed()
        {
            var registry = CreateRegistry("Order", new EntityGdprPolicy
            {
                StorageRules = new[] { new FieldGdprRule { FieldName = "Name", Action = GdprFieldAction.Mask } }
            });
            _gdprProcessor = new GdprProcessor(registry);
            _pipeline = new AuditPipeline(_userResolver, _sourceResolver, _correlationProvider, _gdprProcessor, _store);

            var tx = BuildTransaction(BuildEntry());

            await _pipeline.ProcessAsync(tx);

            Assert.AreEqual(GdprStorageState.PartiallyProcessed, tx.GdprState);
            Assert.IsTrue(tx.Entries.First().Properties.First().NewValue.Contains("*"));
        }

        [TestMethod]
        public async Task ProcessAsync_AllAnonymizeRules_SetsFullyAnonymized()
        {
            var registry = CreateRegistry("Order", new EntityGdprPolicy
            {
                StorageRules = new[] { new FieldGdprRule { FieldName = "Name", Action = GdprFieldAction.Anonymize } }
            });
            _gdprProcessor = new GdprProcessor(registry);
            _pipeline = new AuditPipeline(_userResolver, _sourceResolver, _correlationProvider, _gdprProcessor, _store);

            var tx = BuildTransaction(BuildEntry());

            await _pipeline.ProcessAsync(tx);

            Assert.AreEqual(GdprStorageState.FullyAnonymized, tx.GdprState);
        }

        [TestMethod]
        public async Task ProcessAsync_AllExcludeRules_SetsFullyAnonymized()
        {
            var registry = CreateRegistry("Order", new EntityGdprPolicy
            {
                StorageRules = new[] { new FieldGdprRule { FieldName = "Name", Action = GdprFieldAction.Exclude } }
            });
            _gdprProcessor = new GdprProcessor(registry);
            _pipeline = new AuditPipeline(_userResolver, _sourceResolver, _correlationProvider, _gdprProcessor, _store);

            var tx = BuildTransaction(BuildEntry());

            await _pipeline.ProcessAsync(tx);

            Assert.AreEqual(GdprStorageState.FullyAnonymized, tx.GdprState);
        }

        [TestMethod]
        public async Task ProcessAsync_MixedMaskAndAnonymize_SetsPartiallyProcessed()
        {
            var registry = CreateRegistry("Order", new EntityGdprPolicy
            {
                StorageRules = new[]
                {
                    new FieldGdprRule { FieldName = "Name", Action = GdprFieldAction.Mask },
                    new FieldGdprRule { FieldName = "Email", Action = GdprFieldAction.Anonymize }
                }
            });
            _gdprProcessor = new GdprProcessor(registry);
            _pipeline = new AuditPipeline(_userResolver, _sourceResolver, _correlationProvider, _gdprProcessor, _store);

            var entry = BuildEntryWithProperties("Order",
                Prop("Name", "Alice", "Bob"),
                Prop("Email", "a@b.com", "c@d.com"));
            var tx = BuildTransaction(entry);

            await _pipeline.ProcessAsync(tx);

            Assert.AreEqual(GdprStorageState.PartiallyProcessed, tx.GdprState);
        }

        [TestMethod]
        public async Task ProcessAsync_MultipleEntries_OneNotFullyAnonymized_SetsPartiallyProcessed()
        {
            var registry = CreateRegistry("Order", new EntityGdprPolicy
            {
                StorageRules = new[]
                {
                    new FieldGdprRule { FieldName = "Name", Action = GdprFieldAction.Anonymize },
                    new FieldGdprRule { FieldName = "Email", Action = GdprFieldAction.Hash }
                }
            });
            _gdprProcessor = new GdprProcessor(registry);
            _pipeline = new AuditPipeline(_userResolver, _sourceResolver, _correlationProvider, _gdprProcessor, _store);

            var entry1 = BuildEntryWithProperties("Order", Prop("Name", "Alice", "Bob"));
            var entry2 = BuildEntryWithProperties("Order", Prop("Email", "a@b.com", "c@d.com"));
            var tx = BuildTransaction(entry1, entry2);

            await _pipeline.ProcessAsync(tx);

            Assert.AreEqual(GdprStorageState.PartiallyProcessed, tx.GdprState);
        }

        [TestMethod]
        public async Task ProcessAsync_MultipleEntries_AllFullyAnonymized_SetsFullyAnonymized()
        {
            var registry = CreateRegistry("Order", new EntityGdprPolicy
            {
                StorageRules = new[]
                {
                    new FieldGdprRule { FieldName = "Name", Action = GdprFieldAction.Anonymize },
                    new FieldGdprRule { FieldName = "Email", Action = GdprFieldAction.Exclude }
                }
            });
            _gdprProcessor = new GdprProcessor(registry);
            _pipeline = new AuditPipeline(_userResolver, _sourceResolver, _correlationProvider, _gdprProcessor, _store);

            var entry1 = BuildEntryWithProperties("Order", Prop("Name", "Alice", "Bob"));
            var entry2 = BuildEntryWithProperties("Order", Prop("Email", "a@b.com", "c@d.com"));
            var tx = BuildTransaction(entry1, entry2);

            await _pipeline.ProcessAsync(tx);

            Assert.AreEqual(GdprStorageState.FullyAnonymized, tx.GdprState);
        }

        [TestMethod]
        public async Task ProcessAsync_EntriesWithAndWithoutPolicy_OnlyPolicyEntriesCount()
        {
            // "Order" has anonymize policy; "Product" has no policy
            var registry = CreateRegistry("Order", new EntityGdprPolicy
            {
                StorageRules = new[] { new FieldGdprRule { FieldName = "Name", Action = GdprFieldAction.Anonymize } }
            });
            _gdprProcessor = new GdprProcessor(registry);
            _pipeline = new AuditPipeline(_userResolver, _sourceResolver, _correlationProvider, _gdprProcessor, _store);

            var orderEntry = BuildEntryWithProperties("Order", Prop("Name", "Alice", "Bob"));
            var productEntry = BuildEntryWithProperties("Product", Prop("Title", "Widget", "Gadget"));
            var tx = BuildTransaction(orderEntry, productEntry);

            await _pipeline.ProcessAsync(tx);

            Assert.AreEqual(GdprStorageState.FullyAnonymized, tx.GdprState,
                "Only GDPR-covered entries determine the state; Product has no policy so it doesn't downgrade the state.");
        }

        [TestMethod]
        public async Task ProcessAsync_StoreReturnsFailure_PropagatesFailure()
        {
            _store.ShouldFail = true;
            var tx = BuildTransaction(BuildEntry());

            var result = await _pipeline.ProcessAsync(tx);

            Assert.IsFalse(result.IsSuccess);
        }

        [TestMethod]
        public async Task ProcessAsync_UserResolverFails_RecordsUnresolvedSource()
        {
            _userResolver.ShouldFail = true;
            var tx = BuildTransaction(BuildEntry());

            await _pipeline.ProcessAsync(tx);

            Assert.AreEqual(nameof(AuditUserSource.Unresolved), tx.Metadata[AuditMetadataKeys.UserSource]);
        }

        [TestMethod]
        public async Task ProcessAsync_ResolverSucceedsWithNullResponse_RecordsAnonymousSource()
        {
            _userResolver.UserToReturn = null;
            var tx = BuildTransaction(BuildEntry());

            await _pipeline.ProcessAsync(tx);

            Assert.AreEqual(nameof(AuditUserSource.Anonymous), tx.Metadata[AuditMetadataKeys.UserSource]);
        }

        [TestMethod]
        public async Task ProcessAsync_UnresolvedVsAnonymous_AreDistinguishable()
        {
            var unresolvedTx = BuildTransaction(BuildEntry());
            _userResolver.ShouldFail = true;
            await _pipeline.ProcessAsync(unresolvedTx);

            var anonymousTx = BuildTransaction(BuildEntry());
            _userResolver.ShouldFail = false;
            _userResolver.UserToReturn = null;
            await _pipeline.ProcessAsync(anonymousTx);

            Assert.AreNotEqual(
                unresolvedTx.Metadata[AuditMetadataKeys.UserSource],
                anonymousTx.Metadata[AuditMetadataKeys.UserSource]);
            Assert.AreEqual(nameof(AuditUserSource.Unresolved), unresolvedTx.Metadata[AuditMetadataKeys.UserSource]);
            Assert.AreEqual(nameof(AuditUserSource.Anonymous), anonymousTx.Metadata[AuditMetadataKeys.UserSource]);
        }

        [TestMethod]
        public async Task ProcessAsync_ResolverSucceedsWithResponse_RecordsResponseSource()
        {
            _userResolver.UserToReturn = new AuditUserInfo
            {
                UserId = "user-1",
                UserName = "Alice",
                Source = AuditUserSource.ScopeContext
            };
            var tx = BuildTransaction(BuildEntry());

            await _pipeline.ProcessAsync(tx);

            Assert.AreEqual(nameof(AuditUserSource.ScopeContext), tx.Metadata[AuditMetadataKeys.UserSource]);
        }

        [TestMethod]
        public async Task ProcessAsync_NullMetadataDictionary_DoesNotThrow_AndSetsProvenance()
        {
            _userResolver.UserToReturn = new AuditUserInfo
            {
                UserId = "user-1",
                UserName = "Alice",
                Source = AuditUserSource.ScopeContext
            };
            var tx = BuildTransaction(BuildEntry());
            tx.Metadata = null;

            var result = await _pipeline.ProcessAsync(tx);

            Assert.IsTrue(result.IsSuccess);
            Assert.IsNotNull(tx.Metadata);
            Assert.AreEqual(nameof(AuditUserSource.ScopeContext), tx.Metadata[AuditMetadataKeys.UserSource]);
        }

        [TestMethod]
        public async Task ProcessAsync_ResolverReturnsUserWithoutSource_RecordsUnspecifiedAndStillEnrichesUser()
        {
            _userResolver.ShouldFail = false;
            _userResolver.UserToReturn = new AuditUserInfo
            {
                UserId = "legacy-user-7",
                UserName = "Legacy Resolver User"
            };
            var tx = BuildTransaction(BuildEntry());

            // Act
            await _pipeline.ProcessAsync(tx);

            // Assert: undeclared provenance, not failed attribution...
            Assert.AreEqual(
                nameof(AuditUserSource.Unspecified),
                tx.Metadata[AuditMetadataKeys.UserSource],
                "A resolver that returns a user without stamping Source must record Unspecified, not Unresolved.");

            Assert.AreEqual("legacy-user-7", tx.UserId);
            Assert.AreEqual("Legacy Resolver User", tx.UserName);
        }

        [TestMethod]
        public async Task ProcessAsync_UnspecifiedUnresolvedAndAnonymous_AreThreeDistinctRecordedStrings()
        {
            // Arrange + Act: resolver failure -> Unresolved
            var unresolvedTx = BuildTransaction(BuildEntry());
            _userResolver.ShouldFail = true;
            await _pipeline.ProcessAsync(unresolvedTx);

            // Arrange + Act: success with a null response -> Anonymous
            var anonymousTx = BuildTransaction(BuildEntry());
            _userResolver.ShouldFail = false;
            _userResolver.UserToReturn = null;
            await _pipeline.ProcessAsync(anonymousTx);

            // Arrange + Act: success with a response that never declared Source -> Unspecified
            var unspecifiedTx = BuildTransaction(BuildEntry());
            _userResolver.ShouldFail = false;
            _userResolver.UserToReturn = new AuditUserInfo { UserId = "legacy-user-7", UserName = "Legacy Resolver User" };
            await _pipeline.ProcessAsync(unspecifiedTx);

            // Assert
            var unresolvedValue = unresolvedTx.Metadata[AuditMetadataKeys.UserSource];
            var anonymousValue = anonymousTx.Metadata[AuditMetadataKeys.UserSource];
            var unspecifiedValue = unspecifiedTx.Metadata[AuditMetadataKeys.UserSource];

            Assert.AreEqual(nameof(AuditUserSource.Unresolved), unresolvedValue);
            Assert.AreEqual(nameof(AuditUserSource.Anonymous), anonymousValue);
            Assert.AreEqual(nameof(AuditUserSource.Unspecified), unspecifiedValue);

            Assert.AreNotEqual(unresolvedValue, anonymousValue,
                "A failed resolution must not be persisted the same way as a genuinely anonymous action.");
            Assert.AreNotEqual(unresolvedValue, unspecifiedValue,
                "A failed resolution must not be persisted the same way as a resolved user with undeclared provenance.");
            Assert.AreNotEqual(anonymousValue, unspecifiedValue,
                "A genuinely anonymous action must not be persisted the same way as a resolved user with undeclared provenance.");
        }

        [TestMethod]
        public async Task ProcessAsync_ResponseDeclaresHttpContextSource_RecordsHttpContextNotTheDefault()
        {
            _userResolver.ShouldFail = false;
            _userResolver.UserToReturn = new AuditUserInfo
            {
                UserId = "user-2",
                UserName = "Bob",
                Source = AuditUserSource.HttpContext
            };
            var tx = BuildTransaction(BuildEntry());

            await _pipeline.ProcessAsync(tx);

            Assert.AreEqual(
                nameof(AuditUserSource.HttpContext),
                tx.Metadata[AuditMetadataKeys.UserSource],
                "An explicitly stamped Source must be carried through untouched; the default only applies when nothing was declared.");
            Assert.AreNotEqual(AuditUserSource.Unspecified.ToString(), tx.Metadata[AuditMetadataKeys.UserSource]);
        }
    }
}
