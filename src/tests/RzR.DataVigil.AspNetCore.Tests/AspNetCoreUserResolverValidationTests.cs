using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.VisualStudio.TestTools.UnitTesting;
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
    public class AspNetCoreUserResolverValidationTests
    {
        #region Scope precedence

        [TestMethod]
        public void Resolve_ScopeFullyPopulated_HttpContextDifferentUser_AllFieldsComeFromScope()
        {
            // Arrange
            var httpContext = CreateAuthenticatedContext(
                userId: "http-user",
                userName: "HttpUser",
                roles: new[] { "HttpRole" });
            var scope = new AuditScopeContext();
            scope.SetUser(new AuditUserInfo
            {
                UserId = "scope-id",
                UserName = "ScopeName",
                IpAddress = "10.0.0.5",
                Roles = new[] { "ScopeRole" },
                Claims = new Dictionary<string, string> { ["dept"] = "Scope-Dept" }
            });
            var resolver = new AspNetCoreUserResolver(CreateAccessor(httpContext), scope);

            // Act
            var result = resolver.Resolve();

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsSuccess);
            Assert.IsNotNull(result.Response);
            Assert.AreEqual("scope-id", result.Response.UserId);
            Assert.AreEqual("ScopeName", result.Response.UserName);
            Assert.AreEqual("10.0.0.5", result.Response.IpAddress);
            CollectionAssert.AreEqual(new[] { "ScopeRole" }, result.Response.Roles.ToList());
            Assert.AreEqual("Scope-Dept", result.Response.Claims["dept"]);
        }

        [TestMethod]
        public void Resolve_ScopeWithOnlyUserIdSet_HttpContextPresent_ReturnsScopeAsIsWithoutMerging()
        {
            // Arrange
            var httpContext = CreateAuthenticatedContext(
                userId: "http-user",
                userName: "HttpUser",
                roles: new[] { "HttpRole" });
            var scope = new AuditScopeContext();
            scope.SetUser(new AuditUserInfo { UserId = "only-id-set" });
            var resolver = new AspNetCoreUserResolver(CreateAccessor(httpContext), scope);

            // Act
            var result = resolver.Resolve();

            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsSuccess);
            Assert.IsNotNull(result.Response);
            Assert.AreEqual("only-id-set", result.Response.UserId);
            Assert.IsNull(result.Response.UserName);
            Assert.IsNull(result.Response.IpAddress);
            Assert.IsFalse(result.Response.Roles.Contains("HttpRole"));
        }

        [TestMethod]
        public void Resolve_FreshScopeContextNeverSet_AuthenticatedHttpContext_FallsThroughToHttpContextUser()
        {
            var httpContext = CreateAuthenticatedContext(userId: "user-42", userName: "Bob");
            var freshScope = new AuditScopeContext();
            var resolver = new AspNetCoreUserResolver(CreateAccessor(httpContext), freshScope);

            // Act
            var result = resolver.Resolve();

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsSuccess);
            Assert.IsNotNull(result.Response);
            Assert.AreEqual("user-42", result.Response.UserId);
        }

        [TestMethod]
        public void Resolve_ScopeFailureWithNonNullResponse_FallsThroughToHttpContext()
        {
            var httpContext = CreateAuthenticatedContext(userId: "user-42", userName: "Bob");
            var resolver = new AspNetCoreUserResolver(
                CreateAccessor(httpContext), new FailureWithResponseScopeContext());

            // Act
            var result = resolver.Resolve();

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsSuccess);
            Assert.IsNotNull(result.Response);
            Assert.AreEqual("user-42", result.Response.UserId);
        }

        [TestMethod]
        public void Resolve_ScopeGetCurrentUserReturnsBareNull_DoesNotThrow_FallsThroughToHttpContext()
        {
            // Arrange
            var httpContext = CreateAuthenticatedContext(userId: "user-42", userName: "Bob");
            var resolver = new AspNetCoreUserResolver(
                CreateAccessor(httpContext), new NullReturningScopeContext());

            // Act
            var result = resolver.Resolve();

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsSuccess);
            Assert.IsNotNull(result.Response);
            Assert.AreEqual("user-42", result.Response.UserId);
        }

        #endregion

        #region Never-null contract

        [TestMethod]
        public void Resolve_NeverReturnsBareNull_AcrossAllPaths()
        {
            // No scope, no HttpContext
            var noScopeNoHttp = new AspNetCoreUserResolver(
                new HttpContextAccessor { HttpContext = null }, new AuditScopeContext()).Resolve();
            Assert.IsNotNull(noScopeNoHttp);

            // No scope, unauthenticated HttpContext
            var noScopeUnauthHttp = new AspNetCoreUserResolver(
                CreateAccessor(new DefaultHttpContext()), new AuditScopeContext()).Resolve();
            Assert.IsNotNull(noScopeUnauthHttp);

            // No scope, authenticated HttpContext
            var noScopeAuthHttp = new AspNetCoreUserResolver(
                CreateAccessor(CreateAuthenticatedContext()), new AuditScopeContext()).Resolve();
            Assert.IsNotNull(noScopeAuthHttp);

            // Scope set
            var setScope = new AuditScopeContext();
            setScope.SetUser(new AuditUserInfo { UserId = "u" });
            var scopeSet = new AspNetCoreUserResolver(
                new HttpContextAccessor { HttpContext = null }, setScope).Resolve();
            Assert.IsNotNull(scopeSet);

            // Scope failure
            var scopeFailure = new AspNetCoreUserResolver(
                new HttpContextAccessor { HttpContext = null }, new FailureWithResponseScopeContext()).Resolve();
            Assert.IsNotNull(scopeFailure);
        }

        #endregion

        #region UserId precedence

        [TestMethod]
        public void Resolve_UserIdPrecedence_NameIdentifierPresent_UsedOverSubAndName()
        {
            var identity = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "nid-1"),
                new Claim("sub", "sub-1"),
                new Claim(ClaimTypes.Name, "Name1")
            }, "TestAuth");
            var context = BuildAuthenticatedContext(identity);
            var resolver = new AspNetCoreUserResolver(CreateAccessor(context), new AuditScopeContext());

            var result = resolver.Resolve();

            Assert.AreEqual("nid-1", result.Response.UserId);
        }

        [TestMethod]
        public void Resolve_UserIdPrecedence_NoNameIdentifier_SubClaimUsed()
        {
            var identity = new ClaimsIdentity(new[]
            {
                new Claim("sub", "sub-2"),
                new Claim(ClaimTypes.Name, "Name2")
            }, "TestAuth");
            var context = BuildAuthenticatedContext(identity);
            var resolver = new AspNetCoreUserResolver(CreateAccessor(context), new AuditScopeContext());

            var result = resolver.Resolve();

            Assert.AreEqual("sub-2", result.Response.UserId);
        }

        [TestMethod]
        public void Resolve_UserIdPrecedence_NoNameIdentifierNoSub_IdentityNameUsed()
        {
            var identity = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.Name, "Name3")
            }, "TestAuth");
            var context = BuildAuthenticatedContext(identity);
            var resolver = new AspNetCoreUserResolver(CreateAccessor(context), new AuditScopeContext());

            var result = resolver.Resolve();

            Assert.AreEqual("Name3", result.Response.UserId);
        }

        [TestMethod]
        public void Resolve_UserIdPrecedence_EmptyStringNameIdentifier_UsedAsIs_NotFallenThrough()
        {
            var identity = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, string.Empty),
                new Claim("sub", "sub-4")
            }, "TestAuth");
            var context = BuildAuthenticatedContext(identity);
            var resolver = new AspNetCoreUserResolver(CreateAccessor(context), new AuditScopeContext());

            var result = resolver.Resolve();

            Assert.AreEqual(string.Empty, result.Response.UserId);
        }

        [TestMethod]
        public void Resolve_AuthenticatedIdentity_NoNameClaimsAtAll_UserIdAndUserNameNullButResponseSucceeds()
        {
            var identity = new ClaimsIdentity(Enumerable.Empty<Claim>(), "TestAuth");
            var context = BuildAuthenticatedContext(identity);
            var resolver = new AspNetCoreUserResolver(CreateAccessor(context), new AuditScopeContext());

            var result = resolver.Resolve();

            Assert.IsNotNull(result.Response);
            Assert.IsTrue(result.IsSuccess);
            Assert.IsNull(result.Response.UserId);
            Assert.IsNull(result.Response.UserName);
        }

        #endregion

        #region Roles

        [TestMethod]
        public void Resolve_AuthenticatedWithZeroRoleClaims_RolesIsNonNullAndEmpty()
        {
            var httpContext = CreateAuthenticatedContext();
            var resolver = new AspNetCoreUserResolver(CreateAccessor(httpContext), new AuditScopeContext());

            var result = resolver.Resolve();

            Assert.IsNotNull(result.Response.Roles);
            Assert.AreEqual(0, result.Response.Roles.Count());
        }

        [TestMethod]
        public void Resolve_DuplicateRoleClaims_AreNotDeduplicated()
        {
            var httpContext = CreateAuthenticatedContext(roles: new[] { "Admin", "Admin" });
            var resolver = new AspNetCoreUserResolver(CreateAccessor(httpContext), new AuditScopeContext());

            var result = resolver.Resolve();

            var roles = result.Response.Roles.ToList();
            Assert.AreEqual(2, roles.Count(r => r == "Admin"));
        }

        [TestMethod]
        public void Resolve_RoleClaimTypeAndLiteralRoleString_BothIncludedInRoles()
        {
            var httpContext = CreateAuthenticatedContext(
                roles: new[] { "Admin" },
                extraClaims: new Dictionary<string, string> { ["role"] = "Manager" });
            var resolver = new AspNetCoreUserResolver(CreateAccessor(httpContext), new AuditScopeContext());

            var result = resolver.Resolve();

            var roles = result.Response.Roles.ToList();
            CollectionAssert.Contains(roles, "Admin");
            CollectionAssert.Contains(roles, "Manager");
        }

        [TestMethod]
        public void Resolve_CustomRoleClaimType_ClaimAppearsInRoles_NotInClaims()
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, "u-custom"),
                new Claim(ClaimTypes.Name, "CustomUser"),
                new Claim("roles", "Editor")
            };
            var identity = new ClaimsIdentity(claims, "TestAuth", ClaimTypes.Name, "roles");
            var context = BuildAuthenticatedContext(identity);
            var resolver = new AspNetCoreUserResolver(CreateAccessor(context), new AuditScopeContext());

            var result = resolver.Resolve();

            CollectionAssert.Contains(result.Response.Roles.ToList(), "Editor");
            Assert.IsFalse(result.Response.Claims.ContainsKey("roles"));
        }

        #endregion

        #region Claims

        [TestMethod]
        public void Resolve_ExtraClaim_AppearsInClaimsDictionary_RoleClaimsExcluded()
        {
            var httpContext = CreateAuthenticatedContext(
                roles: new[] { "Admin" },
                extraClaims: new Dictionary<string, string> { ["dept"] = "IT" });
            var resolver = new AspNetCoreUserResolver(CreateAccessor(httpContext), new AuditScopeContext());

            var result = resolver.Resolve();

            Assert.AreEqual("IT", result.Response.Claims["dept"]);
            Assert.IsFalse(result.Response.Claims.ContainsKey(ClaimTypes.Role));
        }

        [TestMethod]
        public void Resolve_DuplicateClaimType_FirstValueWins()
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, "u-dup"),
                new Claim(ClaimTypes.Name, "DupUser"),
                new Claim("dept", "IT"),
                new Claim("dept", "Finance")
            };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            var context = BuildAuthenticatedContext(identity);
            var resolver = new AspNetCoreUserResolver(CreateAccessor(context), new AuditScopeContext());

            var result = resolver.Resolve();

            Assert.AreEqual("IT", result.Response.Claims["dept"]);
        }

        [TestMethod]
        public void Resolve_AuthenticatedWithEmptyClaimsCollection_RolesAndClaimsAreNonNull()
        {
            var identity = new ClaimsIdentity(Enumerable.Empty<Claim>(), "TestAuth");
            var context = BuildAuthenticatedContext(identity);
            var resolver = new AspNetCoreUserResolver(CreateAccessor(context), new AuditScopeContext());

            var result = resolver.Resolve();

            Assert.IsNotNull(result.Response.Roles);
            Assert.IsNotNull(result.Response.Claims);
            Assert.AreEqual(0, result.Response.Roles.Count());
            Assert.AreEqual(0, result.Response.Claims.Count);
        }

        #endregion

        #region Detached snapshot (deferred-enumeration safety)

        [TestMethod]
        public void Resolve_ResultIsDetachedFromHttpContext_RolesAndClaimsSurviveHttpContextBeingNulledOut()
        {
            // Arrange
            var httpContext = CreateAuthenticatedContext(
                roles: new[] { "Admin", "User" },
                extraClaims: new Dictionary<string, string> { ["dept"] = "IT" });
            var accessor = CreateAccessor(httpContext);
            var resolver = new AspNetCoreUserResolver(accessor, new AuditScopeContext());

            // Act
            var result = resolver.Resolve();
            accessor.HttpContext = null;

            var firstPass = result.Response.Roles.ToList();
            var secondPass = result.Response.Roles.ToList();
            CollectionAssert.AreEquivalent(new[] { "Admin", "User" }, firstPass);
            CollectionAssert.AreEquivalent(new[] { "Admin", "User" }, secondPass);
            Assert.AreEqual("IT", result.Response.Claims["dept"]);
        }

        #endregion

        #region IpAddress

        [TestMethod]
        public void Resolve_ExplicitRemoteIpAddress_ReturnedAsIpAddressString()
        {
            var httpContext = CreateAuthenticatedContext();
            httpContext.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.7");
            var resolver = new AspNetCoreUserResolver(CreateAccessor(httpContext), new AuditScopeContext());

            var result = resolver.Resolve();

            Assert.AreEqual("203.0.113.7", result.Response.IpAddress);
        }

        #endregion

        [TestMethod]
        public void Resolve_RoleOnSecondaryIdentityWithOwnRoleClaimType_IsClassifiedAsRole()
        {
            var primary = new ClaimsIdentity(
                new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, "u-1"),
                    new Claim(ClaimTypes.Name, "Alice")
                },
                "PrimaryAuth");

            var secondary = new ClaimsIdentity(
                new[] { new Claim("groups", "Editor") },
                "SecondaryAuth", ClaimTypes.Name, "groups");

            var context = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new[] { primary, secondary })
            };
            var resolver = new AspNetCoreUserResolver(CreateAccessor(context), new AuditScopeContext());

            var result = resolver.Resolve();

            CollectionAssert.Contains(result.Response.Roles.ToList(), "Editor");
            Assert.IsFalse(
                result.Response.Claims.ContainsKey("groups"),
                "A role claim on a secondary identity must not also land in the Claims dictionary.");
        }

        [TestMethod]
        public void Resolve_ScopeUserExplicitlySetButEmpty_IsTrusted_DoesNotFallThroughToHttpContext()
        {
            var scope = new AuditScopeContext();
            scope.SetUser(new AuditUserInfo());
            var httpContext = CreateAuthenticatedContext(userId: "http-user", userName: "HttpAlice");
            var resolver = new AspNetCoreUserResolver(CreateAccessor(httpContext), scope);

            var result = resolver.Resolve();

            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsSuccess);
            Assert.IsNotNull(result.Response);
            Assert.IsNull(result.Response.UserId, "Must not be back-filled from HttpContext.");
            Assert.IsNull(result.Response.UserName, "Must not be back-filled from HttpContext.");
        }

        #region Test helpers

        private static DefaultHttpContext BuildAuthenticatedContext(ClaimsIdentity identity)
        {
            return new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
        }

        private sealed class FailureWithResponseScopeContext : IAuditScopeContext
        {
            public IResult SetUser(AuditUserInfo user) => Result.Success();

            public IResult<AuditUserInfo> GetCurrentUser()
            {
                var failure = Result<AuditUserInfo>.Failure("scope lookup failed");
                failure.Response = new AuditUserInfo { UserId = "should-not-be-used" };

                return failure;
            }

            public void Dispose()
            {
            }
        }

        private sealed class NullReturningScopeContext : IAuditScopeContext
        {
            public IResult SetUser(AuditUserInfo user) => Result.Success();

            public IResult<AuditUserInfo> GetCurrentUser() => null;

            public void Dispose()
            {
            }
        }

        #endregion
    }
}
