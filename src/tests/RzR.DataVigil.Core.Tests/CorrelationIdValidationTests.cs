using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RzR.DataVigil.Abstractions.Constants;
using RzR.DataVigil.Abstractions.Models.Identity;
using RzR.DataVigil.Abstractions.Services;
using RzR.DataVigil.Core.Gdpr;
using RzR.DataVigil.Core.Pipeline;
using RzR.DataVigil.Core.Resolvers;
using RzR.DataVigil.Core.Tests.Resolvers;
using RzR.DataVigil.Core.Tests.Stubs;
using RzR.ResultMessage;
using RzR.ResultMessage.Abstractions;
using static RzR.DataVigil.Core.Tests.Helpers.AuditTestDataBuilder;

namespace RzR.DataVigil.Core.Tests
{
    [TestClass]
    public class CorrelationIdValidationTests
    {
        private static readonly string OverLongValue = new string('a', 300);

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

        #region Over-long scope value (the blocker)

        [TestMethod]
        public void GetCorrelationId_ScopeValueLongerThanLimit_FallsThroughToW3CTraceId()
        {
            // Arrange
            var expectedTraceId = StartW3CActivity();
            var scope = new AuditScopeContext();
            scope.SetCorrelationId(OverLongValue);
            var provider = new DefaultCorrelationProvider(scope);

            // Act
            var result = provider.GetCorrelationId();

            // Assert
            Assert.IsTrue(result.IsSuccess);
            Assert.AreNotEqual(OverLongValue, result.Response,
                "A 300-char scope value overflows the correlation id column and must never be returned.");
            Assert.AreEqual(expectedTraceId, result.Response,
                "The rejected scope value must fall through to the ambient W3C trace id.");
        }

        [TestMethod]
        public void GetCorrelationId_ScopeValueLongerThanLimit_NoAmbientTrace_ReturnsNullRatherThanTheLongValue()
        {
            // Arrange
            var scope = new AuditScopeContext();
            scope.SetCorrelationId(OverLongValue);
            var provider = new DefaultCorrelationProvider(scope);

            // Act
            var result = provider.GetCorrelationId();

            // Assert
            Assert.IsTrue(result.IsSuccess, "Rejection must not turn into a failed result.");
            Assert.IsNull(result.Response,
                "With no next source available the correct answer is null, not a truncated value.");
        }

        #endregion

        #region Disallowed charset

        [TestMethod]
        public void GetCorrelationId_ScopeValueContainingComma_FallsThroughToW3CTraceId()
        {
            // Arrange
            var expectedTraceId = StartW3CActivity();
            var scope = new AuditScopeContext();
            scope.SetCorrelationId("corr-1,corr-2");
            var provider = new DefaultCorrelationProvider(scope);

            // Act
            var result = provider.GetCorrelationId();

            // Assert
            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(expectedTraceId, result.Response);
        }

        [TestMethod]
        public void GetCorrelationId_ScopeValueContainingCarriageReturnAndLineFeed_FallsThroughToW3CTraceId()
        {
            // Arrange
            var expectedTraceId = StartW3CActivity();
            var scope = new AuditScopeContext();
            scope.SetCorrelationId("corr-1\r\nX-Injected: yes");
            var provider = new DefaultCorrelationProvider(scope);

            // Act
            var result = provider.GetCorrelationId();

            // Assert
            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(expectedTraceId, result.Response,
                "An embedded CR/LF must be rejected outright, not stripped and partially honoured.");
        }

        [TestMethod]
        public void GetCorrelationId_WhitespaceOnlyScopeValue_FallsThroughToW3CTraceId()
        {
            // Arrange
            var expectedTraceId = StartW3CActivity();
            var scope = new AuditScopeContext();
            scope.SetCorrelationId("   ");
            var provider = new DefaultCorrelationProvider(scope);

            // Act
            var result = provider.GetCorrelationId();

            // Assert
            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(expectedTraceId, result.Response,
                "Worker half of the whitespace-parity pair; the web half lives in AspNetCore.Tests.");
        }

        #endregion

        #region Length boundary

        [TestMethod]
        public void GetCorrelationId_ScopeValueOfExactlyTheMaximumLength_IsAccepted()
        {
            // Arrange
            StartW3CActivity();
            var atLimit = new string('a', AuditColumnLengths.CorrelationId);
            var scope = new AuditScopeContext();
            scope.SetCorrelationId(atLimit);
            var provider = new DefaultCorrelationProvider(scope);

            // Act
            var result = provider.GetCorrelationId();

            // Assert
            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(atLimit, result.Response,
                "A value of exactly the column length is inclusive and must still be honoured.");
        }

        [TestMethod]
        public void GetCorrelationId_ScopeValueOneCharOverTheMaximumLength_IsRejected()
        {
            // Arrange
            var expectedTraceId = StartW3CActivity();
            var overLimit = new string('a', AuditColumnLengths.CorrelationId + 1);
            var scope = new AuditScopeContext();
            scope.SetCorrelationId(overLimit);
            var provider = new DefaultCorrelationProvider(scope);

            // Act
            var result = provider.GetCorrelationId();

            // Assert
            Assert.IsTrue(result.IsSuccess);
            Assert.AreNotEqual(overLimit, result.Response);
            Assert.AreEqual(expectedTraceId, result.Response);
        }

        #endregion

        #region Normal path must keep working

        [TestMethod]
        public void GetCorrelationId_ScopeValueUsingEveryAllowedPunctuationChar_IsAccepted()
        {
            // Arrange
            StartW3CActivity();
            const string allowed = "svc-1.node_2:00-ff";
            var scope = new AuditScopeContext();
            scope.SetCorrelationId(allowed);
            var provider = new DefaultCorrelationProvider(scope);

            // Act
            var result = provider.GetCorrelationId();

            // Assert
            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(allowed, result.Response,
                "'.', '_', ':' and '-' are inside the allowed charset and must not be rejected.");
        }

        [TestMethod]
        public void GetCorrelationId_ScopeValuePaddedWithWhitespace_IsTrimmedAndAccepted()
        {
            // Arrange
            StartW3CActivity();
            var scope = new AuditScopeContext();
            scope.SetCorrelationId("  corr-123  ");
            var provider = new DefaultCorrelationProvider(scope);

            // Act
            var result = provider.GetCorrelationId();

            // Assert
            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual("corr-123", result.Response,
                "Surrounding whitespace is trimmed rather than causing a charset rejection.");
        }

        #endregion

        #region Throwing scope context

        [TestMethod]
        public void GetCorrelationId_ScopeContextThrows_ReturnsFailureInsteadOfPropagating()
        {
            // Arrange
            var scope = new ThrowingScopeContext();
            var provider = new DefaultCorrelationProvider(scope);

            // Act
            var result = provider.GetCorrelationId();

            // Assert
            Assert.IsNotNull(result);
            Assert.IsFalse(result.IsSuccess,
                "A scope context that throws must be reported as a failed lookup, not unwound to the caller.");
            Assert.AreEqual(1, scope.GetCorrelationIdCallCount);
        }

        [TestMethod]
        public async Task ProcessAsync_ScopeContextThrowsDuringCorrelationLookup_AuditRecordIsStillPersisted()
        {
            // Arrange
            var store = new StubAuditStore();
            var pipeline = new AuditPipeline(
                new StubUserResolver
                {
                    UserToReturn = new AuditUserInfo { UserId = "user-1", UserName = "Alice" }
                },
                new StubSourceResolver { SourceToReturn = "Worker" },
                new DefaultCorrelationProvider(new ThrowingScopeContext()),
                new GdprProcessor(new GdprPolicyRegistry()),
                store);
            var transaction = BuildTransaction(BuildEntry());

            // Act
            var result = await pipeline.ProcessAsync(transaction);

            // Assert
            Assert.IsTrue(result.IsSuccess,
                "A broken scope context must degrade the correlation id, not discard the audit record.");
            Assert.AreEqual(1, store.SaveCallCount);
            Assert.IsNull(transaction.CorrelationId,
                "The failed lookup contributes no correlation id rather than a bogus one.");
        }

        [TestMethod]
        public void GetTraceId_ScopeContextThrows_SucceedsBecauseTheScopeIsNeverConsulted()
        {
            // Arrange
            var expectedTraceId = StartW3CActivity();
            var scope = new ThrowingScopeContext();
            var provider = new DefaultCorrelationProvider(scope);

            // Act
            var result = provider.GetTraceId();

            // Assert
            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(expectedTraceId, result.Response);
            Assert.AreEqual(0, scope.GetCorrelationIdCallCount,
                "GetTraceId reads only the ambient Activity; it must never touch the scope context.");
        }

        #endregion

        #region Test helpers

        private string StartW3CActivity()
        {
            _activity = new Activity("correlation-id-validation-test");
            _activity.SetIdFormat(ActivityIdFormat.W3C);
            _activity.Start();

            return _activity.TraceId.ToHexString();
        }

        private sealed class ThrowingScopeContext : IAuditScopeContext
        {
            public int GetCorrelationIdCallCount { get; private set; }

            public IResult SetUser(AuditUserInfo user) => Result.Success();

            public IResult<AuditUserInfo> GetCurrentUser() => Result<AuditUserInfo>.Success(null);

            public IResult SetCorrelationId(string correlationId) => Result.Success();

            public IResult<string> GetCurrentCorrelationId()
            {
                GetCorrelationIdCallCount++;

                throw new ObjectDisposedException(nameof(ThrowingScopeContext));
            }

            public void Dispose()
            {
            }
        }

        #endregion
    }
}
