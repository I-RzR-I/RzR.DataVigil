using Microsoft.VisualStudio.TestTools.UnitTesting;
using RzR.DataVigil.Abstractions.Models.Identity;
using RzR.DataVigil.Core.Resolvers;

namespace RzR.DataVigil.Core.Tests
{
    [TestClass]
    public class AuditScopeContextTests
    {
        [TestMethod]
        public void SetUser_GetCurrentUser_RoundTrip()
        {
            using (var ctx = new AuditScopeContext())
            {
                var user = new AuditUserInfo { UserId = "u-1", UserName = "Alice" };

                ctx.SetUser(user);
                var result = ctx.GetCurrentUser();

                Assert.IsTrue(result.IsSuccess);
                Assert.AreEqual("u-1", result.Response.UserId);
                Assert.AreEqual("Alice", result.Response.UserName);
            }
        }

        [TestMethod]
        public void GetCurrentUser_BeforeSet_ReturnsNullResponse()
        {
            using (var ctx = new AuditScopeContext())
            {
                var result = ctx.GetCurrentUser();

                Assert.IsTrue(result.IsSuccess);
                Assert.IsNull(result.Response);
            }
        }

        [TestMethod]
        public void Dispose_KeepsTheScopeValues_SoWorkStillInFlightKeepsWhatItDeclared()
        {
            var ctx = new AuditScopeContext();
            ctx.SetUser(new AuditUserInfo { UserId = "u-1", UserName = "Alice" });
            ctx.SetCorrelationId("job-42");

            ctx.Dispose();

            var user = ctx.GetCurrentUser();
            var correlationId = ctx.GetCurrentCorrelationId();

            Assert.IsTrue(user.IsSuccess);
            Assert.IsNotNull(user.Response);
            Assert.AreEqual("u-1", user.Response.UserId);
            Assert.AreEqual("Alice", user.Response.UserName);
            Assert.IsTrue(correlationId.IsSuccess);
            Assert.AreEqual("job-42", correlationId.Response);
        }

        [TestMethod]
        public void Dispose_CalledTwice_DoesNotThrow()
        {
            var ctx = new AuditScopeContext();
            ctx.SetUser(new AuditUserInfo { UserId = "u-1" });
            ctx.SetCorrelationId("job-42");

            ctx.Dispose();
            ctx.Dispose();

            var user = ctx.GetCurrentUser();
            var correlationId = ctx.GetCurrentCorrelationId();

            Assert.IsTrue(user.IsSuccess);
            Assert.AreEqual("u-1", user.Response.UserId);
            Assert.IsTrue(correlationId.IsSuccess);
            Assert.AreEqual("job-42", correlationId.Response);
        }

        [TestMethod]
        public void SetUser_Null_ClearsTheScopeUser()
        {
            using (var ctx = new AuditScopeContext())
            {
                ctx.SetUser(new AuditUserInfo { UserId = "u-1" });

                var setResult = ctx.SetUser(null);
                var result = ctx.GetCurrentUser();

                Assert.IsTrue(setResult.IsSuccess);
                Assert.IsTrue(result.IsSuccess);
                Assert.IsNull(result.Response);
            }
        }

        [TestMethod]
        public void SetCorrelationId_Null_ClearsTheScopeCorrelationId()
        {
            using (var ctx = new AuditScopeContext())
            {
                ctx.SetCorrelationId("job-42");

                var setResult = ctx.SetCorrelationId(null);
                var result = ctx.GetCurrentCorrelationId();

                Assert.IsTrue(setResult.IsSuccess);
                Assert.IsTrue(result.IsSuccess);
                Assert.IsNull(result.Response);
            }
        }

        [TestMethod]
        public void SetUser_ReturnsSuccess()
        {
            using (var ctx = new AuditScopeContext())
            {
                var result = ctx.SetUser(new AuditUserInfo { UserId = "u-1" });

                Assert.IsTrue(result.IsSuccess);
            }
        }

        [TestMethod]
        public void SetUser_OverwritesPrevious()
        {
            using (var ctx = new AuditScopeContext())
            {
                ctx.SetUser(new AuditUserInfo { UserId = "u-1" });
                ctx.SetUser(new AuditUserInfo { UserId = "u-2" });

                var result = ctx.GetCurrentUser();
                Assert.AreEqual("u-2", result.Response.UserId);
            }
        }
    }
}
