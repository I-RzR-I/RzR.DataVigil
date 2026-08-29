using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RzR.DataVigil.Abstractions.Enums;
using RzR.DataVigil.Abstractions.Extensions;
using RzR.DataVigil.Abstractions.Models.Identity;
using RzR.DataVigil.Core.Gdpr;
using RzR.DataVigil.Core.Pipeline;
using RzR.DataVigil.Core.Tests.Resolvers;
using RzR.DataVigil.Core.Tests.Stubs;
using RzR.DataVigil.TestSupport;
using static RzR.DataVigil.TestSupport.AuditTestDataBuilder;

namespace RzR.DataVigil.Core.Tests
{
    [TestClass]
    public class AuditPipelineCancellationTests
    {
        private StubUserResolver _userResolver;
        private StubSourceResolver _sourceResolver;
        private StubCorrelationProvider _correlationProvider;
        private CancelingAuditStore _store;
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
            _sourceResolver = new StubSourceResolver { SourceToReturn = "WebApi" };
            _correlationProvider = new StubCorrelationProvider { CorrelationId = "c-1", TraceId = "t-1" };
            _store = new CancelingAuditStore();
            _gdprProcessor = new GdprProcessor(new GdprPolicyRegistry());
        }

        [TestMethod]
        public async Task ProcessAsync_TheCallersOwnTokenWasCanceled_ReportsCancellationAndLogsNothing()
        {
            var logger = new RecordingLogger<AuditPipeline>();
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var result = await Build(logger).ProcessAsync(BuildTransaction(BuildPipelineEntry()), cts.Token);

            Assert.IsFalse(result.IsSuccess);
            Assert.IsTrue(result.IsAuditCanceled(),
                "A caller demotes its own report on this, so the classification is the contract.");
            Assert.AreEqual(0, logger.Entries.Count,
                "A shutdown is an expected outcome and must not fill the log with errors.");
            Assert.AreEqual(1, _store.SaveCallCount);
        }

        [TestMethod]
        public async Task ProcessAsync_CancellationRaisedWhileTheCallersTokenIsNotCanceled_IsAGenuineFailure()
        {
            var logger = new RecordingLogger<AuditPipeline>();

            var result = await Build(logger).ProcessAsync(BuildTransaction(BuildPipelineEntry()),
                CancellationToken.None);

            Assert.IsFalse(result.IsSuccess);
            Assert.IsFalse(result.IsAuditCanceled(),
                "Cancellation nobody asked for is a defect and must stay visible as one.");
            Assert.AreEqual(1, logger.Entries.Count);
            Assert.AreEqual(LogLevel.Error, logger.Entries[0].Level);
        }

        [TestMethod]
        public async Task ProcessAsync_SucceededNormally_IsNotClassifiedAsCanceled()
        {
            var store = new StubAuditStore();
            var pipeline = new AuditPipeline(
                _userResolver, _sourceResolver, _correlationProvider, _gdprProcessor, store);

            var result = await pipeline.ProcessAsync(BuildTransaction(BuildPipelineEntry()));

            Assert.IsTrue(result.IsSuccess);
            Assert.IsFalse(result.IsAuditCanceled());
        }

        [TestMethod]
        public void IsAuditCanceled_NullResult_IsFalse()
            => Assert.IsFalse(AuditResultExtensions.IsAuditCanceled(null));

        private AuditPipeline Build(ILogger<AuditPipeline> logger)
            => new AuditPipeline(
                _userResolver, _sourceResolver, _correlationProvider, _gdprProcessor, _store, null, logger);
    }
}
