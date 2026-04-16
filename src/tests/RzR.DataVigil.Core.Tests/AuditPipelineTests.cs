using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
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
    }
}
