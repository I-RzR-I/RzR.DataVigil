using System.Security.Claims;
using System.Security.Principal;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RzR.DataVigil.Abstractions.Enums;
using RzR.DataVigil.Abstractions.Models.Identity;
using RzR.DataVigil.Abstractions.Services;
using RzR.DataVigil.Core.Resolvers;
using RzR.DataVigil.Core.Tests.Stubs;
using RzR.ResultMessage;
using RzR.ResultMessage.Abstractions;
using RzR.ResultMessage.Extensions.Result;

namespace RzR.DataVigil.Core.Tests
{
    [TestClass]
    public class DefaultUserResolverFallbackTests
    {
        private IPrincipal _originalPrincipal;

        [TestInitialize]
        public void TestInitialize()
        {
            _originalPrincipal = Thread.CurrentPrincipal;
        }

        [TestCleanup]
        public void TestCleanup()
        {
            Thread.CurrentPrincipal = _originalPrincipal;
        }

        [TestMethod]
        public void GenericIdentity_ConstructedWithNonEmptyName_IsAuthenticatedIsTrue()
        {
            var identity = new GenericIdentity("principal-user");

            Assert.IsTrue(identity.IsAuthenticated);
        }

        [TestMethod]
        public void GenericIdentity_ConstructedWithEmptyName_IsAuthenticatedIsFalse()
        {
            var identity = new GenericIdentity("");

            Assert.IsFalse(identity.IsAuthenticated);
        }

        [TestMethod]
        public void ClaimsIdentity_WithAuthenticationTypeAndNoNameClaim_IsAuthenticatedTrueButNameIsNull()
        {
            var identity = new ClaimsIdentity("TestAuth");

            Assert.IsTrue(identity.IsAuthenticated);
            Assert.IsNull(identity.Name);
        }

        [TestMethod]
        public void ResultValidate_WithFailingPredicate_FlipsIsSuccessButPreservesResponse()
        {
            var user = new AuditUserInfo { UserId = "leaked-failed-user" };

            var result = Result<AuditUserInfo>.Success(user).Validate(_ => false, "forced failure for test");

            Assert.IsFalse(result.IsSuccess);
            Assert.IsNotNull(result.Response);
            Assert.AreEqual("leaked-failed-user", result.Response.UserId);
        }

        [TestMethod]
        public void Resolve_NoScopeUser_AuthenticatedPrincipal_FallsBackToPrincipal()
        {

            var scope = new AuditScopeContext();
            var resolver = new DefaultUserResolver(scope);
            Thread.CurrentPrincipal = new GenericPrincipal(new GenericIdentity("principal-user"), null);

            var result = resolver.Resolve();

            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsSuccess);
            Assert.IsNotNull(result.Response);
            Assert.AreEqual("principal-user", result.Response.UserId);
            Assert.AreEqual("principal-user", result.Response.UserName);
        }

        [TestMethod]
        public void Resolve_NoScopeUser_NullPrincipal_ReturnsSuccessWithNullResponse()
        {

            var scope = new AuditScopeContext();
            var resolver = new DefaultUserResolver(scope);
            Thread.CurrentPrincipal = null;

            var result = resolver.Resolve();

            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsSuccess);
            Assert.IsNull(result.Response);
        }

        [TestMethod]
        public void Resolve_NoScopeUser_UnauthenticatedPrincipal_ReturnsSuccessWithNullResponse()
        {

            var scope = new AuditScopeContext();
            var resolver = new DefaultUserResolver(scope);
            Thread.CurrentPrincipal = new GenericPrincipal(new GenericIdentity(""), null);

            var result = resolver.Resolve();

            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsSuccess);
            Assert.IsNull(result.Response);
        }

        [TestMethod]
        public void Resolve_ScopeUserSet_DifferentAuthenticatedPrincipalAlsoSet_ScopeTakesPrecedence()
        {

            var scope = new AuditScopeContext();
            scope.SetUser(new AuditUserInfo { UserId = "scope-user", UserName = "Scope" });
            var resolver = new DefaultUserResolver(scope);
            Thread.CurrentPrincipal = new GenericPrincipal(new GenericIdentity("principal-user"), null);

            var result = resolver.Resolve();

            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual("scope-user", result.Response.UserId);
        }

        [TestMethod]
        public void Resolve_NoScopeUser_AuthenticatedPrincipalWithNullName_ResponseHasNullUserIdAndUserName()
        {

            var scope = new AuditScopeContext();
            var resolver = new DefaultUserResolver(scope);
            Thread.CurrentPrincipal = new ClaimsPrincipal(new ClaimsIdentity("TestAuth"));

            var result = resolver.Resolve();

            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsSuccess);
            Assert.IsNotNull(result.Response);
            Assert.IsNull(result.Response.UserId);
            Assert.IsNull(result.Response.UserName);
        }

        [TestMethod]
        public void Resolve_ScopeReturnsFailureWithNonNullResponse_DoesNotTrustFailedLookup_FallsBackToPrincipal()
        {

            var scope = new FailingScopeContextWithResponse(new AuditUserInfo { UserId = "leaked-failed-user" });
            var resolver = new DefaultUserResolver(scope);
            Thread.CurrentPrincipal = new GenericPrincipal(new GenericIdentity("principal-user"), null);

            var result = resolver.Resolve();

            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual("principal-user", result.Response.UserId);
        }

        [TestMethod]
        public void Resolve_ScopeReturnsBareNull_DoesNotThrow_FallsBackToPrincipal()
        {

            var scope = new NullReturningScopeContext();
            var resolver = new DefaultUserResolver(scope);
            Thread.CurrentPrincipal = new GenericPrincipal(new GenericIdentity("principal-user"), null);

            var result = resolver.Resolve();

            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual("principal-user", result.Response.UserId);
        }

        [TestMethod]
        public void Resolve_ScopeUserSetThenDisposed_StillResolvesTheScopeUser()
        {

            var scope = new AuditScopeContext();
            scope.SetUser(new AuditUserInfo { UserId = "scope-user" });
            scope.Dispose();
            var resolver = new DefaultUserResolver(scope);
            Thread.CurrentPrincipal = null;

            var result = resolver.Resolve();

            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsSuccess);
            Assert.IsNotNull(result.Response);
            Assert.AreEqual("scope-user", result.Response.UserId);
            Assert.AreEqual(AuditUserSource.ScopeContext, result.Response.Source);
        }
    }
}
