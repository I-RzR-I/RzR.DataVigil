using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RzR.DataVigil.AspNetCore.Resolvers;
using static RzR.DataVigil.AspNetCore.Tests.Helpers.HttpContextHelper;

namespace RzR.DataVigil.AspNetCore.Tests
{
    [TestClass]
    public class AspNetCoreCorrelationProviderTests
    {
        [TestMethod]
        public void GetCorrelationId_CorrelationHeader_ReturnsIt()
        {
            var accessor = CreateAccessorWithHeaders(correlationId: "corr-123");
            var provider = new AspNetCoreCorrelationProvider(accessor);

            var result = provider.GetCorrelationId();

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual("corr-123", result.Response);
        }

        [TestMethod]
        public void GetCorrelationId_RequestIdHeader_FallsBack()
        {
            var accessor = CreateAccessorWithHeaders(requestId: "req-456");
            var provider = new AspNetCoreCorrelationProvider(accessor);

            var result = provider.GetCorrelationId();

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual("req-456", result.Response);
        }

        [TestMethod]
        public void GetCorrelationId_CorrelationTakesPriority()
        {
            var accessor = CreateAccessorWithHeaders(correlationId: "corr-first", requestId: "req-second");
            var provider = new AspNetCoreCorrelationProvider(accessor);

            var result = provider.GetCorrelationId();

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual("corr-first", result.Response);
        }

        [TestMethod]
        public void GetCorrelationId_NoHeaders_FallsBackToRequestTraceIdentifier()
        {
            Activity.Current = null;
            var accessor = CreateAccessorWithHeaders();
            var provider = new AspNetCoreCorrelationProvider(accessor);

            var result = provider.GetCorrelationId();

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(accessor.HttpContext.TraceIdentifier, result.Response);
        }

        [TestMethod]
        public void GetCorrelationId_NoHttpContext_FallsBackToActivity()
        {
            Activity.Current = null;
            var accessor = new HttpContextAccessor { HttpContext = null };
            var provider = new AspNetCoreCorrelationProvider(accessor);

            var result = provider.GetCorrelationId();

            Assert.IsTrue(result.IsSuccess);
            Assert.IsNull(result.Response);
        }

        [TestMethod]
        public void GetTraceId_NoW3CActivity_ReturnsNull()
        {
            Activity.Current = null;

            var context = new DefaultHttpContext();
            Assert.IsFalse(string.IsNullOrEmpty(context.TraceIdentifier));

            var accessor = new HttpContextAccessor { HttpContext = context };
            var provider = new AspNetCoreCorrelationProvider(accessor);

            var result = provider.GetTraceId();

            Assert.IsTrue(result.IsSuccess);
            Assert.IsNull(result.Response);
        }

        [TestMethod]
        public void GetTraceId_NoHttpContext_FallsBackToActivity()
        {
            Activity.Current = null;
            var accessor = new HttpContextAccessor { HttpContext = null };
            var provider = new AspNetCoreCorrelationProvider(accessor);

            var result = provider.GetTraceId();

            Assert.IsTrue(result.IsSuccess);
        }
    }
}
