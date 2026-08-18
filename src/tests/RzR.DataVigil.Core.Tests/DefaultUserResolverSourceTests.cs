using System.Security.Principal;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RzR.DataVigil.Abstractions.Enums;
using RzR.DataVigil.Abstractions.Models.Identity;
using RzR.DataVigil.Core.Resolvers;

namespace RzR.DataVigil.Core.Tests
{
    [TestClass]
    public class DefaultUserResolverSourceTests
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
        public void Resolve_WithScopeUser_StampsScopeContextSource()
        {
            var scope = new AuditScopeContext();
            scope.SetUser(new AuditUserInfo { UserId = "scope-user", UserName = "Scope" });
            var resolver = new DefaultUserResolver(scope);

            var result = resolver.Resolve();

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(AuditUserSource.ScopeContext, result.Response.Source);
        }

        [TestMethod]
        public void Resolve_NoScopeUser_AuthenticatedPrincipal_StampsThreadPrincipalSource()
        {
            var scope = new AuditScopeContext();
            var resolver = new DefaultUserResolver(scope);
            Thread.CurrentPrincipal = new GenericPrincipal(new GenericIdentity("principal-user"), null);

            var result = resolver.Resolve();

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(AuditUserSource.ThreadPrincipal, result.Response.Source);
        }

        [TestMethod]
        public void Resolve_NoScopeUser_NoPrincipal_ReturnsAnonymous_WithNoResponseToStamp()
        {
            var scope = new AuditScopeContext();
            var resolver = new DefaultUserResolver(scope);
            Thread.CurrentPrincipal = null;

            var result = resolver.Resolve();

            Assert.IsTrue(result.IsSuccess);
            Assert.IsNull(result.Response);
        }
    }
}
