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
        public void Dispose_NullsCurrentUser()
        {
            var ctx = new AuditScopeContext();
            ctx.SetUser(new AuditUserInfo { UserId = "u-1" });

            ctx.Dispose();

            var result = ctx.GetCurrentUser();
            Assert.IsNull(result.Response);
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
