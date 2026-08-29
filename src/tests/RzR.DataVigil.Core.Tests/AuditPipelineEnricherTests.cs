using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RzR.DataVigil.Abstractions.Constants;
using RzR.DataVigil.Abstractions.Enums;
using RzR.DataVigil.Abstractions.Models.Entries;
using RzR.DataVigil.Abstractions.Models.Identity;
using RzR.DataVigil.Abstractions.Services;
using RzR.DataVigil.Core.Extensions;
using RzR.DataVigil.Core.Gdpr;
using RzR.DataVigil.Core.Pipeline;
using RzR.DataVigil.Core.Tests.Resolvers;
using RzR.DataVigil.Core.Tests.Stubs;
using RzR.ResultMessage;
using RzR.ResultMessage.Abstractions;
using static RzR.DataVigil.TestSupport.AuditTestDataBuilder;
using static RzR.DataVigil.Core.Tests.Stubs.StubMetadataEnricher;

namespace RzR.DataVigil.Core.Tests
{
    [TestClass]
    public class AuditPipelineEnricherTests
    {
        private const string CustomKey = "tenant";

        private StubUserResolver _userResolver;
        private StubSourceResolver _sourceResolver;
        private StubCorrelationProvider _correlationProvider;
        private StubAuditStore _store;
        private GdprProcessor _gdprProcessor;

        [TestInitialize]
        public void Init()
        {
            _userResolver = new StubUserResolver
            {
                UserToReturn = new AuditUserInfo
                {
                    UserId = "user-1",
                    UserName = "Alice",
                    Source = AuditUserSource.HttpContext
                }
            };
            _sourceResolver = new StubSourceResolver
            {
                SourceToReturn = "WebApi"
            };
            _correlationProvider = new StubCorrelationProvider
            {
                CorrelationId = "c-1",
                TraceId = "t-1"
            };
            _store = new StubAuditStore();
            _gdprProcessor = new GdprProcessor(new GdprPolicyRegistry());
        }

        [TestMethod]
        public async Task ProcessAsync_EnricherPairs_AreCopiedIntoMetadata()
        {
            var pipeline = Build(new StubMetadataEnricher(
                Pair(AuditMetadataKeys.HttpMethod, "POST"),
                Pair(AuditMetadataKeys.HttpRoute, "/orders/{id}")));
            var transaction = BuildTransaction(BuildPipelineEntry());

            var result = await pipeline.ProcessAsync(transaction);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual("POST", transaction.Metadata[AuditMetadataKeys.HttpMethod]);
            Assert.AreEqual("/orders/{id}", transaction.Metadata[AuditMetadataKeys.HttpRoute]);
            Assert.AreSame(transaction, _store.LastSaved);
        }

        [TestMethod]
        public async Task ProcessAsync_MetadataDictionaryWasNull_EnricherPairsStillLand()
        {
            var pipeline = Build(new StubMetadataEnricher(Pair(AuditMetadataKeys.HttpMethod, "GET")));
            var transaction = BuildTransaction(BuildPipelineEntry());
            transaction.Metadata = null;

            var result = await pipeline.ProcessAsync(transaction);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual("GET", transaction!.Metadata![AuditMetadataKeys.HttpMethod]);
        }

        [TestMethod]
        public async Task ProcessAsync_ConsumerSuppliedMetadata_IsPreservedAlongsideEnricherPairs()
        {
            var pipeline = Build(new StubMetadataEnricher(Pair(AuditMetadataKeys.HttpMethod, "GET")));
            var transaction = BuildTransaction(BuildPipelineEntry());
            transaction.Metadata[CustomKey] = "acme";

            await pipeline.ProcessAsync(transaction);

            Assert.AreEqual("acme", transaction.Metadata[CustomKey]);
            Assert.AreEqual("GET", transaction.Metadata[AuditMetadataKeys.HttpMethod]);
        }

        [TestMethod]
        public async Task ProcessAsync_EnricherThrows_TransactionIsStillPersistedAndOtherEnrichersStillRun()
        {
            var throwing = new StubMetadataEnricher
            {
                Outcome = EnrichOutcome.Throw
            };
            var healthy = new StubMetadataEnricher(Pair(AuditMetadataKeys.HttpRoute, "/orders/{id}"));
            var pipeline = Build(throwing, healthy);
            var transaction = BuildTransaction(BuildPipelineEntry());

            var result = await pipeline.ProcessAsync(transaction);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(1, _store.SaveCallCount);
            Assert.AreEqual(1, healthy.EnrichCallCount);
            Assert.AreEqual("/orders/{id}", transaction.Metadata[AuditMetadataKeys.HttpRoute]);
            Assert.AreEqual(nameof(AuditUserSource.HttpContext), transaction.Metadata[AuditMetadataKeys.UserSource]);
        }

        [DataTestMethod]
        [DataRow(EnrichOutcome.FailedResult)]
        [DataRow(EnrichOutcome.NullResult)]
        [DataRow(EnrichOutcome.SuccessWithNullPayload)]
        public async Task ProcessAsync_EnricherReturnsAnUnusableResult_IsSkippedAndTheRecordSurvives(
            EnrichOutcome outcome)
        {
            var unusable = new StubMetadataEnricher {
                Outcome = outcome };
            var healthy = new StubMetadataEnricher(Pair(AuditMetadataKeys.HttpMethod, "PUT"));
            var pipeline = Build(unusable, healthy);
            var transaction = BuildTransaction(BuildPipelineEntry());

            var result = await pipeline.ProcessAsync(transaction);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(1, _store.SaveCallCount);
            Assert.AreEqual("PUT", transaction.Metadata[AuditMetadataKeys.HttpMethod]);
        }

        [TestMethod]
        public async Task ProcessAsync_NullEntryInTheEnricherCollection_IsSkipped()
        {
            var healthy = new StubMetadataEnricher(Pair(AuditMetadataKeys.HttpMethod, "GET"));
            var pipeline = Build(null, healthy);
            var transaction = BuildTransaction(BuildPipelineEntry());

            var result = await pipeline.ProcessAsync(transaction);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual("GET", transaction.Metadata[AuditMetadataKeys.HttpMethod]);
        }

        [TestMethod]
        public async Task ProcessAsync_EnricherEmitsABlankKey_ThePairIsIgnored()
        {
            var pipeline = Build(new StubMetadataEnricher(
                Pair(null, "orphan"),
                Pair(string.Empty, "orphan"),
                Pair(AuditMetadataKeys.HttpMethod, "GET")));
            var transaction = BuildTransaction(BuildPipelineEntry());

            var result = await pipeline.ProcessAsync(transaction);

            Assert.IsTrue(result.IsSuccess);
            Assert.IsFalse(transaction.Metadata.ContainsKey(string.Empty));
            Assert.AreEqual("GET", transaction.Metadata[AuditMetadataKeys.HttpMethod]);
        }

        [TestMethod]
        public async Task ProcessAsync_EnricherEmitsTheUserSourceKey_ThePipelineValueWins()
        {
            var pipeline = Build(new StubMetadataEnricher(
                Pair(AuditMetadataKeys.UserSource, "Forged")));
            var transaction = BuildTransaction(BuildPipelineEntry());

            await pipeline.ProcessAsync(transaction);

            Assert.AreEqual(nameof(AuditUserSource.HttpContext), transaction.Metadata[AuditMetadataKeys.UserSource]);
            Assert.AreNotEqual("Forged", transaction.Metadata[AuditMetadataKeys.UserSource]);
        }

        [TestMethod]
        public async Task ProcessAsync_TwoEnrichersEmitTheSameKey_TheLaterRegistrationWins()
        {
            var pipeline = Build(
                new StubMetadataEnricher(Pair(AuditMetadataKeys.HttpMethod, "first")),
                new StubMetadataEnricher(Pair(AuditMetadataKeys.HttpMethod, "second")));
            var transaction = BuildTransaction(BuildPipelineEntry());

            await pipeline.ProcessAsync(transaction);

            Assert.AreEqual("second", transaction.Metadata[AuditMetadataKeys.HttpMethod]);
        }

        [TestMethod]
        public async Task ProcessAsync_FiveArgumentConstructor_StampsNoHttpKeysAndStillPersists()
        {
            var pipeline = new AuditPipeline(
                _userResolver, _sourceResolver, _correlationProvider, _gdprProcessor, _store);
            var transaction = BuildTransaction(BuildPipelineEntry());

            var result = await pipeline.ProcessAsync(transaction);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(1, _store.SaveCallCount);
            Assert.IsFalse(transaction.Metadata.ContainsKey(AuditMetadataKeys.HttpMethod));
            Assert.IsFalse(transaction.Metadata.ContainsKey(AuditMetadataKeys.HttpRoute));
        }

        [TestMethod]
        public async Task ProcessAsync_NullEnricherCollection_IsTreatedAsEmpty()
        {
            var pipeline = new AuditPipeline(
                _userResolver, _sourceResolver, _correlationProvider, _gdprProcessor, _store, null);
            var transaction = BuildTransaction(BuildPipelineEntry());

            var result = await pipeline.ProcessAsync(transaction);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(1, _store.SaveCallCount);
        }

        [TestMethod]
        public async Task NonWebHost_ContainerResolvedPipeline_HasNoEnrichersAndStampsNoHttpKeys()
        {
            var store = new StubAuditStore();

            var services = new ServiceCollection();
            services.AddSingleton<IAuditStore>(store);
            services.AddAuditTrail(_ => { });

            using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();

            Assert.AreEqual(0, scope.ServiceProvider.GetServices<IAuditMetadataEnricher>().Count(),
                "Nothing registers an enricher in a non-web host.");

            var pipeline = scope.ServiceProvider.GetRequiredService<AuditPipeline>();
            var transaction = BuildTransaction(BuildPipelineEntry());

            var result = await pipeline.ProcessAsync(transaction);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(1, store.SaveCallCount);
            Assert.IsFalse(transaction.Metadata.ContainsKey(AuditMetadataKeys.HttpMethod));
            Assert.IsFalse(transaction.Metadata.ContainsKey(AuditMetadataKeys.HttpRoute));
        }

        [TestMethod]
        public async Task ProcessAsync_TransactionWithNoEntries_DoesNotRunEnrichers()
        {
            var enricher = new StubMetadataEnricher(Pair(AuditMetadataKeys.HttpMethod, "GET"));
            var pipeline = Build(enricher);
            var transaction = new AuditTransaction
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTimeOffset.UtcNow,
                Entries = new List<AuditEntry>()
            };

            var result = await pipeline.ProcessAsync(transaction);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(0, enricher.EnrichCallCount);
            Assert.AreEqual(0, _store.SaveCallCount);
        }

        [TestMethod]
        public async Task ProcessAsync_EnricherReturnsADeferredSequenceThatThrowsWhenEnumerated_RecordSurvives()
        {
            var deferred = new DeferredThrowingEnricher();
            var good = new StubMetadataEnricher(Pair("k.after", "v"));
            var pipeline = Build(deferred, good);
            var transaction = BuildTransaction(BuildPipelineEntry());

            var result = await pipeline.ProcessAsync(transaction);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(1, _store.SaveCallCount);
            Assert.AreEqual("v", transaction.Metadata["k.after"]);
            Assert.IsTrue(transaction.Metadata.ContainsKey(AuditMetadataKeys.UserSource));
        }

        private AuditPipeline Build(params IAuditMetadataEnricher[] enrichers)
            => new AuditPipeline(
                _userResolver, _sourceResolver, _correlationProvider, _gdprProcessor, _store, enrichers);
    }
}
