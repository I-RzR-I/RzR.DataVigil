using Microsoft.VisualStudio.TestTools.UnitTesting;
using RzR.DataVigil.Abstractions.Models.Identity;
using RzR.DataVigil.Core.Resolvers;

namespace RzR.DataVigil.Core.Tests
{
    [TestClass]
    public class DefaultUserResolverTests
    {
        [TestMethod]
        public void Resolve_WithScopeUser_ReturnsScopeUser()
        {
            var scope = new AuditScopeContext();
            scope.SetUser(new AuditUserInfo { UserId = "scope-user", UserName = "Scope" });
            var resolver = new DefaultUserResolver(scope);

            var result = resolver.Resolve();

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual("scope-user", result.Response.UserId);
            Assert.AreEqual("Scope", result.Response.UserName);
        }

        [TestMethod]
        public void Resolve_WithNoScopeUser_ReturnsSuccessWithNullResponse()
        {
            var scope = new AuditScopeContext();
            var resolver = new DefaultUserResolver(scope);

            var result = resolver.Resolve();

            Assert.IsTrue(result.IsSuccess);
        }
    }
}
