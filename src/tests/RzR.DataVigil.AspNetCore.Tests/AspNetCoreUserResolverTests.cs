using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RzR.DataVigil.Abstractions.Models.Identity;
using RzR.DataVigil.AspNetCore.Resolvers;
using RzR.DataVigil.Core.Resolvers;
using static RzR.DataVigil.AspNetCore.Tests.Helpers.HttpContextHelper;

namespace RzR.DataVigil.AspNetCore.Tests
{
    [TestClass]
    public class AspNetCoreUserResolverTests
    {
        [TestMethod]
        public void Resolve_ScopeContextSet_ReturnsScopeUser()
        {
            var httpContext = CreateAuthenticatedContext(userId: "http-user");
            var scope = new AuditScopeContext();
            scope.SetUser(new AuditUserInfo { UserId = "scope-user", UserName = "Scope" });
            var resolver = new AspNetCoreUserResolver(CreateAccessor(httpContext), scope);

            var result = resolver.Resolve();

            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual("scope-user", result.Response.UserId);
        }

        [TestMethod]
        public void Resolve_ScopeOverridesHttpContext()
        {
            var httpContext = CreateAuthenticatedContext(userId: "http-user", userName: "HttpAlice");
            var scope = new AuditScopeContext();
            scope.SetUser(new AuditUserInfo { UserId = "override", UserName = "Override" });
            var resolver = new AspNetCoreUserResolver(CreateAccessor(httpContext), scope);

            var result = resolver.Resolve();

            Assert.AreEqual("override", result.Response.UserId);
            Assert.AreEqual("Override", result.Response.UserName);
        }

        [TestMethod]
        public void Resolve_NoScopeUser_NoHttpContext_ReturnsResultWithNullResponse()
        {
            var accessor = new HttpContextAccessor { HttpContext = null };
            var resolver = new AspNetCoreUserResolver(accessor, new AuditScopeContext());

            var result = resolver.Resolve();

            // No scope user set (anonymous), no HttpContext available — resolver falls through
            // to the anonymous Success() result rather than returning a bare null
            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsSuccess);
            Assert.IsNull(result.Response);
        }

        [TestMethod]
        public void Resolve_NoScopeUser_UnauthenticatedHttpContext_ReturnsResultWithNullResponse()
        {
            var context = new DefaultHttpContext();
            var resolver = new AspNetCoreUserResolver(CreateAccessor(context), new AuditScopeContext());

            var result = resolver.Resolve();

            // Asserts the never-return-bare-null contract: an unauthenticated HttpContext
            // still yields an anonymous, successful Result rather than null
            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsSuccess);
            Assert.IsNull(result.Response);
        }

        [TestMethod]
        public void Resolve_NoScopeUser_AuthenticatedHttpContext_ReturnsHttpContextUser()
        {
            // With no scope user set, the resolver falls through to HttpContext and
            // returns the authenticated user found there
            var httpContext = CreateAuthenticatedContext(userId: "user-42", userName: "Bob");
            var resolver = new AspNetCoreUserResolver(CreateAccessor(httpContext), new AuditScopeContext());

            var result = resolver.Resolve();

            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsSuccess);
            Assert.IsNotNull(result.Response);
            Assert.AreEqual("user-42", result.Response.UserId);
            Assert.AreEqual("Bob", result.Response.UserName);
        }

        [TestMethod]
        public void Resolve_ScopeWithFullUserInfo_AllFieldsReturned()
        {
            var scope = new AuditScopeContext();
            scope.SetUser(new AuditUserInfo
            {
                UserId = "u-99",
                UserName = "FullUser",
                IpAddress = "192.168.1.1",
                Roles = new[] { "Admin", "Manager" },
                Claims = new Dictionary<string, string> { ["dept"] = "IT" }
            });
            var resolver = new AspNetCoreUserResolver(
                new HttpContextAccessor { HttpContext = null }, scope);

            var result = resolver.Resolve();

            Assert.IsNotNull(result);
            Assert.AreEqual("u-99", result.Response.UserId);
            Assert.AreEqual("FullUser", result.Response.UserName);
            Assert.AreEqual("192.168.1.1", result.Response.IpAddress);
            CollectionAssert.AreEqual(
                new[] { "Admin", "Manager" }, result.Response.Roles.ToList());
            Assert.AreEqual("IT", result.Response.Claims["dept"]);
        }
    }
}
