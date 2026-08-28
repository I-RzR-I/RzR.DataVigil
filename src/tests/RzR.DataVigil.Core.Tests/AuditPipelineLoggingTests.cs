using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RzR.DataVigil.Abstractions.Constants;
using RzR.DataVigil.Abstractions.Enums;
using RzR.DataVigil.Abstractions.Models.Entries;
using RzR.DataVigil.Abstractions.Models.Gdpr;
using RzR.DataVigil.Abstractions.Models.Identity;
using RzR.DataVigil.Abstractions.Models.Query;
using RzR.DataVigil.Abstractions.Services;
using RzR.DataVigil.Core.Gdpr;
using RzR.DataVigil.Core.Pipeline;
using RzR.DataVigil.Core.Tests.Resolvers;
using RzR.DataVigil.Core.Tests.Stubs;
using RzR.ResultMessage.Abstractions;
using static RzR.DataVigil.Core.Tests.Helpers.AuditTestDataBuilder;
using static RzR.DataVigil.Core.Tests.Stubs.StubMetadataEnricher;

namespace RzR.DataVigil.Core.Tests
{
    [TestClass]
    public class AuditPipelineLoggingTests
    {
        private const string SensitiveUserName = "Alice-PII-Name";
        private const string SensitiveIpAddress = "203.0.113.77";
        private const string SensitiveOldValue = "OldPersonalDataValue";
        private const string SensitiveNewValue = "NewPersonalDataValue";
        private const string SensitiveEntityName = "PatientRecordEntity";
        private const string StoreFailureMessage = "store exploded";

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
                    UserName = SensitiveUserName,
                    IpAddress = SensitiveIpAddress,
                    Source = AuditUserSource.HttpContext
                }
            };
            _sourceResolver = new StubSourceResolver { SourceToReturn = "WebApi" };
            _correlationProvider = new StubCorrelationProvider { CorrelationId = "c-1", TraceId = "t-1" };
            _store = new StubAuditStore();
            _gdprProcessor = new GdprProcessor(new GdprPolicyRegistry());
        }

        #region Availability

        [TestMethod]
        public async Task ProcessAsync_EnricherThrowsAndTheLogSinkAlsoThrows_TheAuditRecordIsStillPersisted()
        {
            var throwing = new StubMetadataEnricher { Outcome = EnrichOutcome.Throw };
            var pipeline = Build(new ThrowingLogger<AuditPipeline>(), throwing);
            var transaction = BuildTransaction(BuildEntry());

            var result = await pipeline.ProcessAsync(transaction);

            Assert.IsTrue(result.IsSuccess,
                "A dead log sink must not escalate a survivable enricher defect into a lost audit record.");
            Assert.AreEqual(1, _store.SaveCallCount);
        }

        [TestMethod]
        public async Task ProcessAsync_EnricherThrowsAndTheLogSinkAlsoThrows_LaterEnrichersStillRun()
        {
            var throwing = new StubMetadataEnricher { Outcome = EnrichOutcome.Throw };
            var healthy = new StubMetadataEnricher(Pair(AuditMetadataKeys.HttpRoute, "/orders/{id}"));
            var pipeline = Build(new ThrowingLogger<AuditPipeline>(), throwing, healthy);
            var transaction = BuildTransaction(BuildEntry());

            var result = await pipeline.ProcessAsync(transaction);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(1, _store.SaveCallCount);
            Assert.AreEqual("/orders/{id}", transaction.Metadata[AuditMetadataKeys.HttpRoute]);
        }

        [TestMethod]
        public async Task ProcessAsync_NullEntryInTheEnricherCollectionWithALogger_TheAuditRecordIsStillPersisted()
        {
            var logger = new RecordingLogger<AuditPipeline>();
            var healthy = new StubMetadataEnricher(Pair(AuditMetadataKeys.HttpMethod, "GET"));
            var pipeline = Build(logger, null, healthy);
            var transaction = BuildTransaction(BuildEntry());

            var result = await pipeline.ProcessAsync(transaction);

            Assert.IsTrue(result.IsSuccess,
                "Resolving the enricher type name must happen inside the guarded helper, not at the call site.");
            Assert.AreEqual(1, _store.SaveCallCount);
            Assert.AreEqual("GET", transaction.Metadata[AuditMetadataKeys.HttpMethod]);
        }

        #endregion

        #region Enricher failure diagnostics

        [TestMethod]
        public async Task ProcessAsync_EnricherThrows_TheWarningNamesTheEnricherTypeAndCarriesNoPersonalData()
        {
            var logger = new RecordingLogger<AuditPipeline>();
            var throwing = new StubMetadataEnricher { Outcome = EnrichOutcome.Throw };
            var pipeline = Build(logger, throwing);
            var transaction = BuildTransaction(
                BuildEntryWithProperties(SensitiveEntityName,
                    Prop("Name", SensitiveOldValue, SensitiveNewValue)));

            var result = await pipeline.ProcessAsync(transaction);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(1, logger.Entries.Count);
            Assert.AreEqual(LogLevel.Warning, logger.Entries[0].Level);

            var message = logger.Entries[0].Message;
            Assert.IsTrue(message.Contains(typeof(StubMetadataEnricher).FullName),
                "The fully qualified type name is what lets an operator find the offending enricher.");
            Assert.IsFalse(message.Contains(SensitiveUserName));
            Assert.IsFalse(message.Contains(SensitiveIpAddress));
            Assert.IsFalse(message.Contains(SensitiveOldValue));
            Assert.IsFalse(message.Contains(SensitiveNewValue));
            Assert.IsFalse(message.Contains(SensitiveEntityName));
            Assert.IsFalse(message.Contains(transaction.Id.ToString()));
        }

        #endregion

        #region Process failure diagnostics

        [TestMethod]
        public async Task ProcessAsync_StoreThrows_LogsExactlyOneErrorAndStillReturnsTheSameFailure()
        {
            var logger = new RecordingLogger<AuditPipeline>();

            var logged = await RunAgainstAThrowingStoreAsync(logger);
            var control = await RunAgainstAThrowingStoreAsync(null);

            Assert.IsFalse(logged.IsSuccess);
            Assert.IsFalse(control.IsSuccess);
            Assert.AreEqual($"{control.GetFirstMessage()}", $"{logged.GetFirstMessage()}",
                "Attaching a logger reports the failure; it must not alter the failure the caller receives.");
            Assert.AreEqual(1, logger.Entries.Count);
            Assert.AreEqual(LogLevel.Error, logger.Entries[0].Level);
        }

        [TestMethod]
        public async Task ProcessAsync_StoreThrowsAndTheLogSinkAlsoThrows_TheStoreFailureIsWhatIsReturned()
        {
            var result = await RunAgainstAThrowingStoreAsync(new ThrowingLogger<AuditPipeline>());

            Assert.IsFalse(result.IsSuccess);
            Assert.IsFalse(
                $"{result.GetFirstMessage()}".Contains(ThrowingLogger<AuditPipeline>.SinkFailureMessage),
                "A dead sink must not replace the store failure with a logging failure.");
        }

        [TestMethod]
        public async Task ProcessAsync_HealthyRun_LogsNothing()
        {
            var logger = new RecordingLogger<AuditPipeline>();
            var pipeline = Build(logger, new StubMetadataEnricher(Pair(AuditMetadataKeys.HttpMethod, "GET")));
            var transaction = BuildTransaction(BuildEntry());

            var result = await pipeline.ProcessAsync(transaction);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(0, logger.Entries.Count,
                "The ordinary audit write must stay free of diagnostics at any level.");
        }

        #endregion

        #region Test helpers

        private Task<IResult> RunAgainstAThrowingStoreAsync(ILogger<AuditPipeline> logger)
            => new AuditPipeline(
                    _userResolver, _sourceResolver, _correlationProvider, _gdprProcessor,
                    new ThrowingAuditStore(), null, logger)
                .ProcessAsync(BuildTransaction(BuildEntry()));

        private AuditPipeline Build(ILogger<AuditPipeline> logger, params IAuditMetadataEnricher[] enrichers)
            => new AuditPipeline(
                _userResolver, _sourceResolver, _correlationProvider, _gdprProcessor, _store,
                enrichers, logger);

        private sealed class ThrowingAuditStore : IAuditStore
        {
            public Task<IResult> SaveAsync(AuditTransaction transaction,
                CancellationToken cancellationToken = default)
                => throw new InvalidOperationException(StoreFailureMessage);

            public Task<IResult<IEnumerable<AuditTransaction>>> QueryAsync(
                AuditTransactionQuery filters,
                GdprRetrievalContext gdprRetrievalContext = null,
                CancellationToken cancellationToken = default)
                => throw new NotImplementedException();

            public Task<IResult> AnonymizeByUserAsync(string userId,
                CancellationToken cancellationToken = default)
                => throw new NotImplementedException();

            public Task<IResult> PurgeBeforeAsync(DateTimeOffset before,
                CancellationToken cancellationToken = default)
                => throw new NotImplementedException();
        }

        #endregion
    }
}
