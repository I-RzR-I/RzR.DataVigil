using Microsoft.AspNetCore.Http;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RzR.DataVigil.Abstractions.Enums;
using RzR.DataVigil.Abstractions.Models.Identity;
using RzR.DataVigil.AspNetCore.Resolvers;
using RzR.DataVigil.Core.Resolvers;
using static RzR.DataVigil.AspNetCore.Tests.Helpers.HttpContextHelper;

namespace RzR.DataVigil.AspNetCore.Tests
{
    [TestClass]
    public class AspNetCoreUserResolverSourceTests
    {
        [TestMethod]
        public void Resolve_WithScopeUser_StampsScopeContextSource()
        {
            var httpContext = CreateAuthenticatedContext(userId: "http-user");
            var scope = new AuditScopeContext();
            scope.SetUser(new AuditUserInfo { UserId = "scope-user", UserName = "Scope" });
            var resolver = new AspNetCoreUserResolver(CreateAccessor(httpContext), scope);

            var result = resolver.Resolve();

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(AuditUserSource.ScopeContext, result.Response.Source);
        }

        [TestMethod]
        public void Resolve_NoScopeUser_AuthenticatedHttpContext_StampsHttpContextSource()
        {
            var httpContext = CreateAuthenticatedContext(userId: "user-42", userName: "Bob");
            var resolver = new AspNetCoreUserResolver(CreateAccessor(httpContext), new AuditScopeContext());

            var result = resolver.Resolve();

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(AuditUserSource.HttpContext, result.Response.Source);
        }

        [TestMethod]
        public void Resolve_NoScopeUser_NoHttpContext_ReturnsAnonymous_WithNoResponseToStamp()
        {
            var accessor = new HttpContextAccessor { HttpContext = null };
            var resolver = new AspNetCoreUserResolver(accessor, new AuditScopeContext());

            var result = resolver.Resolve();

            Assert.IsTrue(result.IsSuccess);
            Assert.IsNull(result.Response);
        }
    }
}
