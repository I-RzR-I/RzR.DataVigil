using System.Diagnostics;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RzR.DataVigil.Abstractions.Constants;
using RzR.DataVigil.AspNetCore.Resolvers;
using RzR.DataVigil.AspNetCore.Tests.Stubs;
using RzR.DataVigil.Core.Resolvers;

namespace RzR.DataVigil.AspNetCore.Tests
{
    [TestClass]
    public class CorrelationRejectionLoggingTests
    {
        private const string PoisonedValue = "zz-SECRET-BEARER-9f8e7d;\r\nX-Injected: evil";

        private const char OffendingCharacter = ';';

        private const string ValidTraceIdentifier = "0HN7CTQ0Q0OJ4:00000001";

        private static readonly string OverLongValue = new string('a', 300);

        [TestInitialize]
        public void Init()
        {
            Activity.Current = null;
        }

        [TestCleanup]
        public void Cleanup()
        {
            Activity.Current = null;
        }

        #region The level split

        [TestMethod]
        public void GetCorrelationId_SameRejectionFromTheScopeAndFromAHeader_ReportsAtWarningAndAtDebug()
        {
            var scopeLogger = new RecordingLogger<AspNetCoreCorrelationProvider>();
            new AspNetCoreCorrelationProvider(
                    AccessorWith(traceIdentifier: ValidTraceIdentifier),
                    ScopeWith(OverLongValue), scopeLogger)
                .GetCorrelationId();

            var headerLogger = new RecordingLogger<AspNetCoreCorrelationProvider>();
            new AspNetCoreCorrelationProvider(
                    AccessorWith(correlationId: OverLongValue, traceIdentifier: ValidTraceIdentifier),
                    null, headerLogger)
                .GetCorrelationId();

            Assert.AreEqual(1, scopeLogger.Entries.Count);
            Assert.AreEqual(1, headerLogger.Entries.Count);
            Assert.AreEqual(LogLevel.Warning, scopeLogger.Entries[0].Level,
                "A value the developer set is a real defect and must be visible in a default configuration.");
            Assert.AreEqual(LogLevel.Debug, headerLogger.Entries[0].Level,
                "A header value is attacker-drivable; reporting it above Debug hands out a log-volume lever.");
        }

        [TestMethod]
        public void GetCorrelationId_RejectedCorrelationHeader_ReportsAtDebugNamingTheHeader()
        {
            var logger = new RecordingLogger<AspNetCoreCorrelationProvider>();
            var provider = new AspNetCoreCorrelationProvider(
                AccessorWith(correlationId: OverLongValue, traceIdentifier: ValidTraceIdentifier),
                null, logger);

            var result = provider.GetCorrelationId();

            Assert.AreEqual(ValidTraceIdentifier, result.Response);
            Assert.AreEqual(1, logger.Entries.Count);
            Assert.AreEqual(LogLevel.Debug, logger.Entries[0].Level);
            Assert.IsTrue(logger.Entries[0].Message.Contains("X-Correlation-Id header"));
        }

        [TestMethod]
        public void GetCorrelationId_RejectedRequestIdHeader_ReportsAtDebugNamingTheHeader()
        {
            var logger = new RecordingLogger<AspNetCoreCorrelationProvider>();
            var provider = new AspNetCoreCorrelationProvider(
                AccessorWith(requestId: "req,with,commas", traceIdentifier: ValidTraceIdentifier),
                null, logger);

            var result = provider.GetCorrelationId();

            Assert.AreEqual(ValidTraceIdentifier, result.Response);
            Assert.AreEqual(1, logger.Entries.Count);
            Assert.AreEqual(LogLevel.Debug, logger.Entries[0].Level);
            Assert.IsTrue(logger.Entries[0].Message.Contains("X-Request-Id header"));
        }

        [TestMethod]
        public void GetCorrelationId_RejectedHeaderAndDebugIsOff_LogsNothing()
        {
            var logger = new RecordingLogger<AspNetCoreCorrelationProvider>
            {
                MinLevel = LogLevel.Information
            };
            var provider = new AspNetCoreCorrelationProvider(
                AccessorWith(correlationId: OverLongValue, traceIdentifier: ValidTraceIdentifier),
                null, logger);

            provider.GetCorrelationId();

            Assert.AreEqual(0, logger.Entries.Count,
                "A default production configuration emits nothing for attacker-driven rejections.");
        }

        #endregion

        #region Silence

        [DataTestMethod]
        [DataRow("   ")]
        [DataRow("\t")]
        [DataRow(" \t ")]
        public void GetCorrelationId_BlankCorrelationHeader_LogsNothing(string headerValue)
        {
            var logger = new RecordingLogger<AspNetCoreCorrelationProvider>();
            var provider = new AspNetCoreCorrelationProvider(
                AccessorWith(correlationId: headerValue, traceIdentifier: ValidTraceIdentifier),
                null, logger);

            var result = provider.GetCorrelationId();

            Assert.AreEqual(ValidTraceIdentifier, result.Response);
            Assert.AreEqual(0, logger.Entries.Count,
                "A blank header must classify as never-supplied, so it cannot be used to generate log lines.");
        }

        [DataTestMethod]
        [DataRow("   ")]
        [DataRow("\t")]
        public void GetCorrelationId_BlankRequestIdHeader_LogsNothing(string headerValue)
        {
            var logger = new RecordingLogger<AspNetCoreCorrelationProvider>();
            var provider = new AspNetCoreCorrelationProvider(
                AccessorWith(requestId: headerValue, traceIdentifier: ValidTraceIdentifier),
                null, logger);

            var result = provider.GetCorrelationId();

            Assert.AreEqual(ValidTraceIdentifier, result.Response);
            Assert.AreEqual(0, logger.Entries.Count);
        }

        [TestMethod]
        public void GetCorrelationId_AcceptedCorrelationHeader_LogsNothing()
        {
            var logger = new RecordingLogger<AspNetCoreCorrelationProvider>();
            var provider = new AspNetCoreCorrelationProvider(
                AccessorWith(correlationId: "corr-123"), null, logger);

            var result = provider.GetCorrelationId();

            Assert.AreEqual("corr-123", result.Response);
            Assert.AreEqual(0, logger.Entries.Count);
        }

        [TestMethod]
        public void GetCorrelationId_NoScopeNoHeadersAndAnAcceptableTraceIdentifier_LogsNothing()
        {
            var logger = new RecordingLogger<AspNetCoreCorrelationProvider>();
            var provider = new AspNetCoreCorrelationProvider(
                AccessorWith(traceIdentifier: ValidTraceIdentifier), new AuditScopeContext(), logger);

            var result = provider.GetCorrelationId();

            Assert.AreEqual(ValidTraceIdentifier, result.Response);
            Assert.AreEqual(0, logger.Entries.Count,
                "An ordinary request carrying no correlation header must emit nothing at any level.");
        }

        #endregion

        #region The rejected value must never reach a sink

        [TestMethod]
        public void GetCorrelationId_RejectedHeaderValue_NeitherTheValueNorItsControlCharactersAreLogged()
        {
            var logger = new RecordingLogger<AspNetCoreCorrelationProvider>();
            var provider = new AspNetCoreCorrelationProvider(
                AccessorWith(correlationId: PoisonedValue, requestId: PoisonedValue,
                    traceIdentifier: ValidTraceIdentifier),
                ScopeWith(PoisonedValue), logger);

            provider.GetCorrelationId();

            Assert.AreEqual(3, logger.Entries.Count,
                "The three rejections must actually be reported, otherwise the assertions below are vacuous.");
            Assert.IsTrue(logger.Entries.All(e => !e.Message.Contains("SECRET-BEARER")),
                "The rejected value is unvalidated input and may be a bearer token.");
            Assert.IsTrue(logger.Entries.All(e => !e.Message.Contains(PoisonedValue)));
            Assert.IsTrue(logger.Entries.All(e => e.Message.IndexOf(OffendingCharacter) < 0),
                "Not even the offending character may be echoed back.");
            Assert.IsTrue(logger.Entries.All(e => e.Message.IndexOf('\r') < 0));
            Assert.IsTrue(logger.Entries.All(e => e.Message.IndexOf('\n') < 0),
                "A logged CR/LF would let a caller forge log lines.");
        }

        #endregion

        #region Length boundary

        [TestMethod]
        public void GetCorrelationId_CorrelationHeaderOfOneHundredTwentyNineChars_IsAcceptedAndLogsNothing()
        {
            var logger = new RecordingLogger<AspNetCoreCorrelationProvider>();
            var justOverTheOldCap = new string('a', 129);
            var provider = new AspNetCoreCorrelationProvider(
                AccessorWith(correlationId: justOverTheOldCap, traceIdentifier: ValidTraceIdentifier),
                null, logger);

            var result = provider.GetCorrelationId();

            Assert.AreEqual(justOverTheOldCap, result.Response,
                "The cap now follows the storage column, so 129 chars is well inside it.");
            Assert.AreEqual(0, logger.Entries.Count);
        }

        [TestMethod]
        public void GetCorrelationId_CorrelationHeaderOfTwoHundredFiftySevenChars_IsRejected()
        {
            var logger = new RecordingLogger<AspNetCoreCorrelationProvider>();
            var overLimit = new string('a', AuditColumnLengths.CorrelationId + 1);
            var provider = new AspNetCoreCorrelationProvider(
                AccessorWith(correlationId: overLimit, traceIdentifier: ValidTraceIdentifier),
                null, logger);

            var result = provider.GetCorrelationId();

            Assert.AreEqual(ValidTraceIdentifier, result.Response,
                "257 chars does not fit the correlation id column and must never be returned.");
            Assert.AreEqual(1, logger.Entries.Count);
            Assert.AreEqual(LogLevel.Debug, logger.Entries[0].Level);
        }

        #endregion

        #region Availability

        [TestMethod]
        public void GetCorrelationId_LogSinkThrowsWhileReportingAHeaderRejection_StillFallsThrough()
        {
            var provider = new AspNetCoreCorrelationProvider(
                AccessorWith(correlationId: OverLongValue, requestId: "req-456"),
                null, new ThrowingLogger<AspNetCoreCorrelationProvider>());

            var result = provider.GetCorrelationId();

            Assert.IsTrue(result.IsSuccess,
                "A broken log sink must never turn a survivable rejection into a failed lookup.");
            Assert.AreEqual("req-456", result.Response);
        }

        [TestMethod]
        public void GetCorrelationId_LogSinkThrowsWhileReportingAScopeRejection_StillFallsThrough()
        {
            var provider = new AspNetCoreCorrelationProvider(
                AccessorWith(correlationId: "corr-123"),
                ScopeWith(OverLongValue), new ThrowingLogger<AspNetCoreCorrelationProvider>());

            var result = provider.GetCorrelationId();

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual("corr-123", result.Response);
        }

        #endregion

        #region Test helpers

        private static AuditScopeContext ScopeWith(string correlationId)
        {
            var scope = new AuditScopeContext();
            scope.SetCorrelationId(correlationId);

            return scope;
        }

        private static IHttpContextAccessor AccessorWith(
            string correlationId = null,
            string requestId = null,
            string traceIdentifier = null)
        {
            var context = new DefaultHttpContext();
            if (correlationId != null)
                context.Request.Headers["X-Correlation-Id"] = correlationId;

            if (requestId != null)
                context.Request.Headers["X-Request-Id"] = requestId;

            if (traceIdentifier != null)
                context.TraceIdentifier = traceIdentifier;

            return new StubHttpContextAccessor { HttpContext = context };
        }

        #endregion
    }
}
