using System;
using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using RzR.DataVigil.AspNetCore.Tests.Stubs;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RzR.DataVigil.Abstractions.Constants;
using RzR.DataVigil.Abstractions.Models.Identity;
using RzR.DataVigil.Abstractions.Services;
using RzR.DataVigil.AspNetCore.Resolvers;
using RzR.DataVigil.Core.Resolvers;
using RzR.ResultMessage;
using RzR.ResultMessage.Abstractions;
using static RzR.DataVigil.AspNetCore.Tests.Helpers.HttpContextHelper;

namespace RzR.DataVigil.AspNetCore.Tests
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
        public void GetCorrelationId_ScopeValueLongerThanLimit_FallsThroughToCorrelationHeader()
        {
            var accessor = CreateAccessorWithHeaders(correlationId: "corr-123");
            var provider = new AspNetCoreCorrelationProvider(accessor, ScopeWith(OverLongValue));

            var result = provider.GetCorrelationId();

            Assert.IsTrue(result.IsSuccess);
            Assert.AreNotEqual(OverLongValue, result.Response,
                "A 300-char scope value overflows the correlation id column and must never be returned.");
            Assert.AreEqual("corr-123", result.Response);
        }

        [TestMethod]
        public void GetCorrelationId_ScopeValueLongerThanLimit_NoHeaders_FallsThroughToTraceIdentifier()
        {
            var accessor = StubAccessor();
            accessor.HttpContext.TraceIdentifier = "0HN7CTQ0Q0OJ4:00000001";
            var provider = new AspNetCoreCorrelationProvider(accessor, ScopeWith(OverLongValue));

            var result = provider.GetCorrelationId();

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual("0HN7CTQ0Q0OJ4:00000001", result.Response);
        }

        #endregion

        #region Disallowed charset

        [TestMethod]
        public void GetCorrelationId_ScopeValueContainingComma_FallsThroughToCorrelationHeader()
        {
            var accessor = CreateAccessorWithHeaders(correlationId: "corr-123");
            var provider = new AspNetCoreCorrelationProvider(accessor, ScopeWith("corr-1,corr-2"));

            var result = provider.GetCorrelationId();

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual("corr-123", result.Response);
        }

        [TestMethod]
        public void GetCorrelationId_ScopeValueContainingCarriageReturnAndLineFeed_FallsThroughToCorrelationHeader()
        {
            var accessor = CreateAccessorWithHeaders(correlationId: "corr-123");
            var provider = new AspNetCoreCorrelationProvider(accessor, ScopeWith("corr-1\r\nX-Injected: yes"));

            var result = provider.GetCorrelationId();

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual("corr-123", result.Response,
                "An embedded CR/LF must be rejected outright, not stripped and partially honoured.");
        }

        [TestMethod]
        public void GetCorrelationId_WhitespaceOnlyScopeValue_FallsThroughToCorrelationHeader()
        {
            var accessor = CreateAccessorWithHeaders(correlationId: "corr-123");
            var provider = new AspNetCoreCorrelationProvider(accessor, ScopeWith("   "));

            var result = provider.GetCorrelationId();

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual("corr-123", result.Response,
                "Web half of the whitespace-parity pair; a blank scope value must be rejected here "
                + "exactly as a worker rejects it.");
        }

        #endregion

        #region Length boundary

        [TestMethod]
        public void GetCorrelationId_ScopeValueOfExactlyTheMaximumLength_IsAccepted()
        {
            var atLimit = new string('a', AuditColumnLengths.CorrelationId);
            var accessor = CreateAccessorWithHeaders(correlationId: "corr-123");
            var provider = new AspNetCoreCorrelationProvider(accessor, ScopeWith(atLimit));

            var result = provider.GetCorrelationId();

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(atLimit, result.Response,
                "A value of exactly the column length is inclusive and must still be honoured.");
        }

        [TestMethod]
        public void GetCorrelationId_ScopeValueOneCharOverTheMaximumLength_IsRejected()
        {
            var overLimit = new string('a', AuditColumnLengths.CorrelationId + 1);
            var accessor = CreateAccessorWithHeaders(correlationId: "corr-123");
            var provider = new AspNetCoreCorrelationProvider(accessor, ScopeWith(overLimit));

            var result = provider.GetCorrelationId();

            Assert.IsTrue(result.IsSuccess);
            Assert.AreNotEqual(overLimit, result.Response);
            Assert.AreEqual("corr-123", result.Response);
        }

        #endregion

        #region Trace identifier

        [TestMethod]
        public void GetCorrelationId_TraceIdentifierLongerThanLimit_FallsThroughToW3CTraceId()
        {
            var expectedTraceId = StartW3CActivity();
            var accessor = CreateAccessorWithHeaders();
            accessor.HttpContext.TraceIdentifier = OverLongValue;
            var provider = new AspNetCoreCorrelationProvider(accessor);

            var result = provider.GetCorrelationId();

            Assert.IsTrue(result.IsSuccess);
            Assert.AreNotEqual(OverLongValue, result.Response,
                "A server-generated trace identifier is still validated before it reaches storage.");
            Assert.AreEqual(expectedTraceId, result.Response);
        }

        [TestMethod]
        public void GetCorrelationId_KestrelShapedTraceIdentifier_IsStillAccepted()
        {
            StartW3CActivity();
            var accessor = StubAccessor();
            accessor.HttpContext.TraceIdentifier = "0HN7CTQ0Q0OJ4:00000001";
            var provider = new AspNetCoreCorrelationProvider(accessor);

            var result = provider.GetCorrelationId();

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual("0HN7CTQ0Q0OJ4:00000001", result.Response,
                "The ':' in a Kestrel trace identifier is inside the allowed charset; rejecting it "
                + "would break the ordinary request path.");
        }

        #endregion

        #region Headers

        [TestMethod]
        public void GetCorrelationId_CorrelationHeaderLongerThanLimit_FallsThroughToRequestIdHeader()
        {
            var accessor = CreateAccessorWithHeaders(correlationId: OverLongValue, requestId: "req-456");
            var provider = new AspNetCoreCorrelationProvider(accessor);

            var result = provider.GetCorrelationId();

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual("req-456", result.Response);
        }

        [TestMethod]
        public void GetCorrelationId_BothHeadersRejected_FallsThroughToTraceIdentifier()
        {
            var accessor = StubAccessor(
                correlationId: "corr,with,commas",
                requestId: OverLongValue);
            accessor.HttpContext.TraceIdentifier = "0HN7CTQ0Q0OJ4:00000002";
            var provider = new AspNetCoreCorrelationProvider(accessor);

            var result = provider.GetCorrelationId();

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual("0HN7CTQ0Q0OJ4:00000002", result.Response);
        }

        #endregion

        #region Throwing scope context

        [TestMethod]
        public void GetCorrelationId_ScopeContextThrows_ReturnsFailureInsteadOfPropagating()
        {
            var accessor = CreateAccessorWithHeaders(correlationId: "corr-123");
            var scope = new ThrowingScopeContext();
            var provider = new AspNetCoreCorrelationProvider(accessor, scope);

            var result = provider.GetCorrelationId();

            Assert.IsNotNull(result);
            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(1, scope.GetCorrelationIdCallCount);
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

        private static AuditScopeContext ScopeWith(string correlationId)
        {
            var scope = new AuditScopeContext();
            scope.SetCorrelationId(correlationId);

            return scope;
        }

        private static IHttpContextAccessor StubAccessor(
            string correlationId = null,
            string requestId = null)
        {
            var context = new DefaultHttpContext();
            if (correlationId != null)
                context.Request.Headers["X-Correlation-Id"] = correlationId;

            if (requestId != null)
                context.Request.Headers["X-Request-Id"] = requestId;

            return new StubHttpContextAccessor { HttpContext = context };
        }

        #endregion
    }
}
