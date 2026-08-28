using System.Diagnostics;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RzR.DataVigil.Abstractions.Constants;
using RzR.DataVigil.Core.Resolvers;
using RzR.DataVigil.Core.Tests.Stubs;

namespace RzR.DataVigil.Core.Tests
{
    [TestClass]
    public class CorrelationRejectionLoggingTests
    {
        private const string PoisonedValue = "zz-SECRET-BEARER-9f8e7d;\r\nX-Injected: evil";

        private const char OffendingCharacter = ';';

        private Activity _activity;

        [TestInitialize]
        public void Init()
        {
            Activity.Current = null;
        }

        [TestCleanup]
        public void Cleanup()
        {
            _activity?.Stop();
            _activity = null;
            Activity.Current = null;
        }

        #region Silence

        [TestMethod]
        public void GetCorrelationId_ScopeCorrelationIdWasNeverSet_LogsNothing()
        {
            var logger = new RecordingLogger<DefaultCorrelationProvider>();
            var provider = new DefaultCorrelationProvider(new AuditScopeContext(), logger);

            var result = provider.GetCorrelationId();

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(0, logger.Entries.Count,
                "A host that never sets a correlation id would otherwise emit a line on every audit write.");
        }

        [TestMethod]
        public void GetCorrelationId_NoScopeContextAtAll_LogsNothing()
        {
            var logger = new RecordingLogger<DefaultCorrelationProvider>();
            var provider = new DefaultCorrelationProvider(null, logger);

            var result = provider.GetCorrelationId();

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(0, logger.Entries.Count);
        }

        [TestMethod]
        public void GetCorrelationId_AcceptedScopeValue_LogsNothing()
        {
            var logger = new RecordingLogger<DefaultCorrelationProvider>();
            var provider = new DefaultCorrelationProvider(ScopeWith("svc-1.node_2:00-ff"), logger);

            var result = provider.GetCorrelationId();

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual("svc-1.node_2:00-ff", result.Response);
            Assert.AreEqual(0, logger.Entries.Count,
                "An accepted correlation id is the ordinary path and must produce no diagnostics.");
        }

        #endregion

        #region Rejection reporting

        [TestMethod]
        public void GetCorrelationId_ScopeValueTooLong_LogsExactlyOneWarningNamingTheScope()
        {
            var logger = new RecordingLogger<DefaultCorrelationProvider>();
            var overLimit = new string('a', AuditColumnLengths.CorrelationId + 1);
            var provider = new DefaultCorrelationProvider(ScopeWith(overLimit), logger);

            provider.GetCorrelationId();

            Assert.AreEqual(1, logger.Entries.Count);
            Assert.AreEqual(LogLevel.Warning, logger.Entries[0].Level,
                "A developer-set value that cannot be stored is a defect, not attacker traffic.");
            Assert.IsTrue(logger.Entries[0].Message.Contains("AuditScope"));
            Assert.IsTrue(logger.Entries[0].Message.Contains(
                AuditColumnLengths.CorrelationId.ToString()));
            Assert.IsTrue(logger.Entries[0].Message.Contains(overLimit.Length.ToString()));
            Assert.IsFalse(logger.Entries[0].Message.Contains(overLimit));
        }

        [TestMethod]
        public void GetCorrelationId_ScopeValueWithADisallowedCharacter_LogsExactlyOneWarning()
        {
            var logger = new RecordingLogger<DefaultCorrelationProvider>();
            var provider = new DefaultCorrelationProvider(ScopeWith("corr-1,corr-2"), logger);

            provider.GetCorrelationId();

            Assert.AreEqual(1, logger.Entries.Count);
            Assert.AreEqual(LogLevel.Warning, logger.Entries[0].Level);
            Assert.IsTrue(logger.Entries[0].Message.Contains("AuditScope"));
            Assert.IsTrue(logger.Entries[0].Message.Contains("6"),
                "The zero-based index of the first disallowed character is the only positional fact reported.");
            Assert.IsTrue(logger.Entries[0].Message.Contains("13"),
                "The candidate length is reported; the candidate itself is not.");
            Assert.IsFalse(logger.Entries[0].Message.Contains("corr-1,corr-2"));
        }

        #endregion

        #region The rejected value must never reach a sink

        [TestMethod]
        public void GetCorrelationId_RejectedScopeValue_NeitherTheValueNorItsControlCharactersAreLogged()
        {
            var logger = new RecordingLogger<DefaultCorrelationProvider>();
            var provider = new DefaultCorrelationProvider(ScopeWith(PoisonedValue), logger);

            provider.GetCorrelationId();

            Assert.AreEqual(1, logger.Entries.Count,
                "The rejection must actually be reported, otherwise the assertions below are vacuous.");
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
        public void GetCorrelationId_ScopeValueOfOneHundredTwentyNineChars_IsAcceptedAndLogsNothing()
        {
            var logger = new RecordingLogger<DefaultCorrelationProvider>();
            var justOverTheOldCap = new string('a', 129);
            var provider = new DefaultCorrelationProvider(ScopeWith(justOverTheOldCap), logger);

            var result = provider.GetCorrelationId();

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(justOverTheOldCap, result.Response,
                "The cap now follows the storage column, so 129 chars is well inside it.");
            Assert.AreEqual(0, logger.Entries.Count);
        }

        [TestMethod]
        public void GetCorrelationId_ScopeValueOfTwoHundredFiftySevenChars_IsRejected()
        {
            var logger = new RecordingLogger<DefaultCorrelationProvider>();
            var overLimit = new string('a', 257);
            var provider = new DefaultCorrelationProvider(ScopeWith(overLimit), logger);

            var result = provider.GetCorrelationId();

            Assert.IsTrue(result.IsSuccess);
            Assert.AreNotEqual(overLimit, result.Response,
                "257 chars does not fit the correlation id column and must never be returned.");
            Assert.AreEqual(1, logger.Entries.Count);
            Assert.AreEqual(LogLevel.Warning, logger.Entries[0].Level);
        }

        #endregion

        #region Availability

        [TestMethod]
        public void GetCorrelationId_LogSinkThrows_TheRejectionStillFallsThroughAndSucceeds()
        {
            var expectedTraceId = StartW3CActivity();
            var provider = new DefaultCorrelationProvider(
                ScopeWith(PoisonedValue), new ThrowingLogger<DefaultCorrelationProvider>());

            var result = provider.GetCorrelationId();

            Assert.IsTrue(result.IsSuccess,
                "A broken log sink must never turn a survivable rejection into a failed lookup.");
            Assert.AreEqual(expectedTraceId, result.Response);
        }

        #endregion

        #region Test helpers

        private string StartW3CActivity()
        {
            _activity = new Activity("correlation-rejection-logging-test");
            _activity.SetIdFormat(ActivityIdFormat.W3C);
            _activity.Start();

            return _activity.TraceId.ToHexString();
        }

        private static AuditScopeContext ScopeWith(string correlationId)
        {
            var scope = new AuditScopeContext();
            scope.SetCorrelationId(correlationId);

            return scope;
        }

        #endregion
    }
}
