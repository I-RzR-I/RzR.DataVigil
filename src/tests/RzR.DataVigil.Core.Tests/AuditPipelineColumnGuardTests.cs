using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RzR.DataVigil.Abstractions.Constants;
using RzR.DataVigil.Abstractions.Enums;
using RzR.DataVigil.Abstractions.Models.Entries;
using RzR.DataVigil.Abstractions.Models.Identity;
using RzR.DataVigil.Abstractions.Services;
using RzR.DataVigil.Core.Gdpr;
using RzR.DataVigil.Core.Pipeline;
using RzR.DataVigil.Core.Tests.Resolvers;
using RzR.DataVigil.Core.Tests.Stubs;
using RzR.DataVigil.TestSupport;
using static RzR.DataVigil.TestSupport.AuditTestDataBuilder;
using static RzR.DataVigil.Core.Tests.Stubs.StubMetadataEnricher;

namespace RzR.DataVigil.Core.Tests
{
    [TestClass]
    public class AuditPipelineColumnGuardTests
    {
        private const string HighSurrogate = "\uD83D";
        private const string LowSurrogate = "\uDE00";
        private const string Emoji = HighSurrogate + LowSurrogate;
        private const string UserNameMarker = "PII-SECRET-USERNAME-";
        private const string ExpectedFullOrder = "UserId,UserName,IpAddress,Source,CorrelationId,TraceId";

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
                    IpAddress = "203.0.113.7",
                    Source = AuditUserSource.HttpContext
                }
            };
            _sourceResolver = new StubSourceResolver { SourceToReturn = "WebApi" };
            _correlationProvider = new StubCorrelationProvider { CorrelationId = "c-1", TraceId = "t-1" };
            _store = new StubAuditStore();
            _gdprProcessor = new GdprProcessor(new GdprPolicyRegistry());
        }

        #region Surrogate safety

        [TestMethod]
        public async Task ProcessAsync_UserNameSplitsASurrogatePairAtTheColumnBoundary_TheStoredValueSurvivesAUtf8RoundTrip()
        {
            _userResolver.UserToReturn.UserName =
                new string('a', AuditColumnLengths.UserName - 1) + Emoji + new string('b', 50);
            var transaction = BuildTransaction(BuildPipelineEntry());

            var result = await Build().ProcessAsync(transaction);

            Assert.IsTrue(result.IsSuccess);

            var written = _store.LastSaved.UserName;

            Assert.AreEqual(AuditColumnLengths.UserName - 1, written.Length,
                "The cut must step back off the high surrogate rather than land between the pair.");
            Assert.IsFalse(char.IsSurrogate(written[written.Length - 1]),
                "A trailing lone surrogate is what fails or corrupts the insert this guard exists to prevent.");
            Assert.AreEqual(written, Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(written)),
                "A lone high surrogate UTF-8 encodes to U+FFFD, so the round trip is the definitive check.");
        }

        [TestMethod]
        public async Task ProcessAsync_SurrogatePairEndsExactlyAtTheColumnBoundary_TheWholePairIsKept()
        {
            _userResolver.UserToReturn.UserName =
                new string('a', AuditColumnLengths.UserName - 2) + Emoji + new string('b', 50);
            var transaction = BuildTransaction(BuildPipelineEntry());

            await Build().ProcessAsync(transaction);

            var written = _store.LastSaved.UserName;

            Assert.AreEqual(AuditColumnLengths.UserName, written.Length,
                "A pair that fits whole must not be dropped by an over-eager step back.");
            Assert.AreEqual(written, Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(written)));
        }

        #endregion

        #region Truncate versus reject

        [TestMethod]
        public async Task ProcessAsync_OverLongUserId_IsTruncatedToTheColumnLengthAndTheRecordIsStillSaved()
        {
            _userResolver.UserToReturn.UserId = new string('u', AuditColumnLengths.UserId + 44);
            var transaction = BuildTransaction(BuildPipelineEntry());

            var result = await Build().ProcessAsync(transaction);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(1, _store.SaveCallCount,
                "Truncating an actor identifier must keep the record, not drop it.");
            Assert.AreEqual(new string('u', AuditColumnLengths.UserId), _store.LastSaved.UserId);
        }

        [TestMethod]
        public async Task ProcessAsync_OverLongTraceId_IsNulledRatherThanTruncatedAndTheRecordIsStillSaved()
        {
            _correlationProvider.TraceId = new string('t', AuditColumnLengths.TraceId + 1);
            var transaction = BuildTransaction(BuildPipelineEntry());

            var result = await Build().ProcessAsync(transaction);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(1, _store.SaveCallCount);
            Assert.IsNull(_store.LastSaved.TraceId,
                "A truncated join key looks valid and joins to nothing, which is false evidence.");
        }

        [TestMethod]
        public async Task ProcessAsync_OverLongCorrelationId_IsNulledRatherThanTruncatedAndTheRecordIsStillSaved()
        {
            _correlationProvider.CorrelationId = new string('c', AuditColumnLengths.CorrelationId + 1);
            var transaction = BuildTransaction(BuildPipelineEntry());

            var result = await Build().ProcessAsync(transaction);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(1, _store.SaveCallCount);
            Assert.IsNull(_store.LastSaved.CorrelationId);
        }

        [TestMethod]
        public async Task ProcessAsync_OverLongSource_IsTruncatedToTheColumnLength()
        {
            _sourceResolver.SourceToReturn = new string('s', AuditColumnLengths.Source + 10);
            var transaction = BuildTransaction(BuildPipelineEntry());

            await Build().ProcessAsync(transaction);

            Assert.AreEqual(new string('s', AuditColumnLengths.Source), _store.LastSaved.Source);
        }

        [TestMethod]
        public async Task ProcessAsync_ValuesExactlyAtTheColumnLength_ArePassedThroughUnguarded()
        {
            _userResolver.UserToReturn.UserId = new string('u', AuditColumnLengths.UserId);
            _correlationProvider.TraceId = new string('t', AuditColumnLengths.TraceId);
            var transaction = BuildTransaction(BuildPipelineEntry());

            await Build().ProcessAsync(transaction);

            Assert.AreEqual(new string('u', AuditColumnLengths.UserId), _store.LastSaved.UserId);
            Assert.AreEqual(new string('t', AuditColumnLengths.TraceId), _store.LastSaved.TraceId);
            Assert.IsFalse(_store.LastSaved.Metadata.ContainsKey(AuditMetadataKeys.Oversize),
                "The boundary length is legal, so nothing was guarded.");
        }

        #endregion

        #region Oversize metadata key

        [TestMethod]
        public async Task ProcessAsync_EverySixGuardedFieldIsOverLong_TheOversizeKeyListsThemInStorageOrder()
        {
            OverflowEveryGuardedField();
            var transaction = BuildTransaction(BuildPipelineEntry());

            await Build().ProcessAsync(transaction);

            Assert.AreEqual(ExpectedFullOrder, _store.LastSaved.Metadata[AuditMetadataKeys.Oversize]);
        }

        [TestMethod]
        public async Task ProcessAsync_ASubsetIsOverLong_OnlyTheAffectedFieldsAppearCommaSeparatedWithNoSpaces()
        {
            _userResolver.UserToReturn.UserName = new string('n', AuditColumnLengths.UserName + 1);
            _sourceResolver.SourceToReturn = new string('s', AuditColumnLengths.Source + 1);
            var transaction = BuildTransaction(BuildPipelineEntry());

            await Build().ProcessAsync(transaction);

            Assert.AreEqual("UserName,Source", _store.LastSaved.Metadata[AuditMetadataKeys.Oversize]);
        }

        [TestMethod]
        public async Task ProcessAsync_OnlyTraceIdWasRejected_ItIsStillReportedInTheOversizeKey()
        {
            _correlationProvider.TraceId = new string('t', AuditColumnLengths.TraceId + 1);
            var transaction = BuildTransaction(BuildPipelineEntry());

            await Build().ProcessAsync(transaction);

            Assert.AreEqual("TraceId", _store.LastSaved.Metadata[AuditMetadataKeys.Oversize],
                "TraceId is assigned after the UserSource line, so an emit point placed there would " +
                "actively remove the key and let the record self-report as clean.");
        }

        [TestMethod]
        public async Task ProcessAsync_OnlyCorrelationIdWasRejected_ItIsStillReportedInTheOversizeKey()
        {
            _correlationProvider.CorrelationId = new string('c', AuditColumnLengths.CorrelationId + 1);
            var transaction = BuildTransaction(BuildPipelineEntry());

            await Build().ProcessAsync(transaction);

            Assert.AreEqual("CorrelationId", _store.LastSaved.Metadata[AuditMetadataKeys.Oversize]);
        }

        [TestMethod]
        public async Task ProcessAsync_NothingWasGuarded_TheOversizeKeyIsAbsent()
        {
            var transaction = BuildTransaction(BuildPipelineEntry());

            await Build().ProcessAsync(transaction);

            Assert.IsFalse(_store.LastSaved.Metadata.ContainsKey(AuditMetadataKeys.Oversize));
        }

        #endregion

        #region Anti-forgery

        [TestMethod]
        public async Task ProcessAsync_EnricherPreSeedsTheOversizeKeyButNothingIsOverLong_TheKeyIsRemoved()
        {
            var hostile = new StubMetadataEnricher(Pair(AuditMetadataKeys.Oversize, "UserId,TraceId"));
            var transaction = BuildTransaction(BuildPipelineEntry());

            await Build(hostile).ProcessAsync(transaction);

            Assert.IsFalse(_store.LastSaved.Metadata.ContainsKey(AuditMetadataKeys.Oversize),
                "A clean record must never be made to self-report as guarded by a pre-seeded key.");
        }

        [TestMethod]
        public async Task ProcessAsync_ConsumerPreSeedsTheOversizeKeyButNothingIsOverLong_TheKeyIsRemoved()
        {
            var transaction = BuildTransaction(BuildPipelineEntry());
            transaction.Metadata[AuditMetadataKeys.Oversize] = "UserId,UserName";

            await Build().ProcessAsync(transaction);

            Assert.IsFalse(_store.LastSaved.Metadata.ContainsKey(AuditMetadataKeys.Oversize));
        }

        [TestMethod]
        public async Task ProcessAsync_EnricherPreSeedsTheOversizeKeyAndOneFieldIsOverLong_ThePipelineValueWins()
        {
            var hostile = new StubMetadataEnricher(Pair(AuditMetadataKeys.Oversize, "UserId,TraceId"));
            _userResolver.UserToReturn.IpAddress = new string('9', AuditColumnLengths.IpAddress + 1);
            var transaction = BuildTransaction(BuildPipelineEntry());

            await Build(hostile).ProcessAsync(transaction);

            Assert.AreEqual("IpAddress", _store.LastSaved.Metadata[AuditMetadataKeys.Oversize]);
        }

        #endregion

        #region Guard diagnostics

        [TestMethod]
        public async Task ProcessAsync_UserNameWasTruncated_NoLogLineCarriesTheGuardedValue()
        {
            var logger = new RecordingLogger<AuditPipeline>();
            _userResolver.UserToReturn.UserName = string.Concat(Enumerable.Repeat(UserNameMarker, 20));
            var transaction = BuildTransaction(BuildPipelineEntry());

            await Build(logger).ProcessAsync(transaction);

            var stored = _store.LastSaved.UserName;

            Assert.AreEqual(1, logger.Entries.Count, "At most one guard line per transaction.");
            Assert.IsTrue(logger.Entries[0].Message.Contains(nameof(AuditTransaction.UserName)),
                "The field name is what makes the report actionable.");

            foreach (var entry in logger.Entries)
            {
                Assert.IsFalse(entry.Message.Contains(UserNameMarker),
                    "The guarded value is attacker controlled personal data and must never reach a log.");
                Assert.IsFalse(entry.Message.Contains(stored),
                    "Logging the truncated prefix leaks the same value.");
            }
        }

        [TestMethod]
        public async Task ProcessAsync_OnlyIdentityFieldsFromHttpContextWereGuarded_TheReportIsDebug()
        {
            var logger = new RecordingLogger<AuditPipeline>();
            _userResolver.UserToReturn.UserId = new string('u', AuditColumnLengths.UserId + 1);
            _userResolver.UserToReturn.IpAddress = new string('9', AuditColumnLengths.IpAddress + 1);
            var transaction = BuildTransaction(BuildPipelineEntry());

            await Build(logger).ProcessAsync(transaction);

            Assert.AreEqual(1, logger.Entries.Count);
            Assert.AreEqual(LogLevel.Debug, logger.Entries[0].Level,
                "Over-long identity attributes arriving from an identity provider are a known condition.");
        }

        [TestMethod]
        public async Task ProcessAsync_AJoinKeyWasGuarded_TheReportIsWarningEvenFromHttpContext()
        {
            var logger = new RecordingLogger<AuditPipeline>();
            _correlationProvider.TraceId = new string('t', AuditColumnLengths.TraceId + 1);
            var transaction = BuildTransaction(BuildPipelineEntry());

            await Build(logger).ProcessAsync(transaction);

            Assert.AreEqual(1, logger.Entries.Count);
            Assert.AreEqual(LogLevel.Warning, logger.Entries[0].Level,
                "A dropped join key is anomalous whatever resolved the actor.");
        }

        [TestMethod]
        public async Task ProcessAsync_IdentityFieldGuardedButTheActorCameFromTheScopeContext_TheReportIsWarning()
        {
            var logger = new RecordingLogger<AuditPipeline>();
            _userResolver.UserToReturn.Source = AuditUserSource.ScopeContext;
            _userResolver.UserToReturn.UserId = new string('u', AuditColumnLengths.UserId + 1);
            var transaction = BuildTransaction(BuildPipelineEntry());

            await Build(logger).ProcessAsync(transaction);

            Assert.AreEqual(1, logger.Entries.Count);
            Assert.AreEqual(LogLevel.Warning, logger.Entries[0].Level);
        }

        [TestMethod]
        public async Task ProcessAsync_AllSixFieldsGuarded_ExactlyOneLineIsLogged()
        {
            var logger = new RecordingLogger<AuditPipeline>();
            OverflowEveryGuardedField();
            var transaction = BuildTransaction(BuildPipelineEntry());

            await Build(logger).ProcessAsync(transaction);

            Assert.AreEqual(1, logger.Entries.Count, "One line per transaction, not one per field.");
        }

        #endregion

        #region Availability

        [TestMethod]
        public async Task ProcessAsync_IdentityFieldGuardedAndTheLogSinkThrows_TheAuditRecordIsStillPersisted()
        {
            _userResolver.UserToReturn.UserName = new string('n', AuditColumnLengths.UserName + 1);
            var transaction = BuildTransaction(BuildPipelineEntry());

            var result = await Build(new ThrowingLogger<AuditPipeline>()).ProcessAsync(transaction);

            Assert.IsTrue(result.IsSuccess,
                "The Debug branch asks IsEnabled first, so a sink that throws there must still be survived.");
            Assert.AreEqual(1, _store.SaveCallCount);
            Assert.AreEqual(new string('n', AuditColumnLengths.UserName), _store.LastSaved.UserName);
        }

        [TestMethod]
        public async Task ProcessAsync_JoinKeyGuardedAndTheLogSinkThrows_TheAuditRecordIsStillPersisted()
        {
            _correlationProvider.TraceId = new string('t', AuditColumnLengths.TraceId + 1);
            var transaction = BuildTransaction(BuildPipelineEntry());

            var result = await Build(new ThrowingLogger<AuditPipeline>()).ProcessAsync(transaction);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(1, _store.SaveCallCount);
            Assert.AreEqual("TraceId", _store.LastSaved.Metadata[AuditMetadataKeys.Oversize]);
        }

        #endregion

        #region Test helpers

        private void OverflowEveryGuardedField()
        {
            _userResolver.UserToReturn.UserId = new string('u', AuditColumnLengths.UserId + 1);
            _userResolver.UserToReturn.UserName = new string('n', AuditColumnLengths.UserName + 1);
            _userResolver.UserToReturn.IpAddress = new string('9', AuditColumnLengths.IpAddress + 1);
            _sourceResolver.SourceToReturn = new string('s', AuditColumnLengths.Source + 1);
            _correlationProvider.CorrelationId = new string('c', AuditColumnLengths.CorrelationId + 1);
            _correlationProvider.TraceId = new string('t', AuditColumnLengths.TraceId + 1);
        }

        private AuditPipeline Build(params IAuditMetadataEnricher[] enrichers)
            => new AuditPipeline(
                _userResolver, _sourceResolver, _correlationProvider, _gdprProcessor, _store, enrichers);

        private AuditPipeline Build(ILogger<AuditPipeline> logger,
            params IAuditMetadataEnricher[] enrichers)
            => new AuditPipeline(
                _userResolver, _sourceResolver, _correlationProvider, _gdprProcessor, _store,
                enrichers, logger);

        #endregion
    }
}
