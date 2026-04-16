using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RzR.DataVigil.Abstractions.Enums;
using RzR.DataVigil.Abstractions.Models.Gdpr;

namespace RzR.DataVigil.Core.Tests
{
    [TestClass]
    public class GdprRetrievalContextTests
    {
        [TestMethod]
        public void CanAccess_NoRolesNoClaims_ReturnsFalse()
        {
            var ctx = new GdprRetrievalContext();
            var rule = new FieldGdprRule
            {
                FieldName = "Email",
                Action = GdprFieldAction.Mask,
                AllowedRoles = new[] { "Admin" }
            };

            Assert.IsFalse(ctx.CanAccess(rule));
        }

        [TestMethod]
        public void CanAccess_MatchingRole_ReturnsTrue()
        {
            var ctx = new GdprRetrievalContext { UserRoles = new[] { "Admin", "User" } };
            var rule = new FieldGdprRule
            {
                FieldName = "Email",
                Action = GdprFieldAction.Mask,
                AllowedRoles = new[] { "Admin" }
            };

            Assert.IsTrue(ctx.CanAccess(rule));
        }

        [TestMethod]
        public void CanAccess_NonMatchingRole_ReturnsFalse()
        {
            var ctx = new GdprRetrievalContext { UserRoles = new[] { "User" } };
            var rule = new FieldGdprRule
            {
                FieldName = "Email",
                Action = GdprFieldAction.Mask,
                AllowedRoles = new[] { "Admin" }
            };

            Assert.IsFalse(ctx.CanAccess(rule));
        }

        [TestMethod]
        public void CanAccess_MatchingClaim_ReturnsTrue()
        {
            var ctx = new GdprRetrievalContext
            {
                UserClaims = new Dictionary<string, string> { ["gdpr"] = "full" }
            };
            var rule = new FieldGdprRule
            {
                FieldName = "SSN",
                Action = GdprFieldAction.Anonymize,
                AllowedClaims = new Dictionary<string, string> { ["gdpr"] = "full" }
            };

            Assert.IsTrue(ctx.CanAccess(rule));
        }

        [TestMethod]
        public void CanAccess_WrongClaimValue_ReturnsFalse()
        {
            var ctx = new GdprRetrievalContext
            {
                UserClaims = new Dictionary<string, string> { ["gdpr"] = "partial" }
            };
            var rule = new FieldGdprRule
            {
                FieldName = "SSN",
                Action = GdprFieldAction.Anonymize,
                AllowedClaims = new Dictionary<string, string> { ["gdpr"] = "full" }
            };

            Assert.IsFalse(ctx.CanAccess(rule));
        }

        [TestMethod]
        public void CanAccess_RoleFailsButClaimPasses_ReturnsTrue()
        {
            var ctx = new GdprRetrievalContext
            {
                UserRoles = new[] { "User" },
                UserClaims = new Dictionary<string, string> { ["gdpr"] = "full" }
            };
            var rule = new FieldGdprRule
            {
                FieldName = "SSN",
                Action = GdprFieldAction.Anonymize,
                AllowedRoles = new[] { "Admin" },
                AllowedClaims = new Dictionary<string, string> { ["gdpr"] = "full" }
            };

            Assert.IsTrue(ctx.CanAccess(rule));
        }

        [TestMethod]
        public void CanAccess_NullAllowedRolesAndClaims_ReturnsFalse()
        {
            var ctx = new GdprRetrievalContext { UserRoles = new[] { "Admin" } };
            var rule = new FieldGdprRule
            {
                FieldName = "Email",
                Action = GdprFieldAction.Mask,
                AllowedRoles = null,
                AllowedClaims = null
            };

            Assert.IsFalse(ctx.CanAccess(rule));
        }

        [TestMethod]
        public void CanAccess_EmptyAllowedRolesAndClaims_ReturnsFalse()
        {
            var ctx = new GdprRetrievalContext { UserRoles = new[] { "Admin" } };
            var rule = new FieldGdprRule
            {
                FieldName = "Email",
                Action = GdprFieldAction.Mask,
                AllowedRoles = new string[0],
                AllowedClaims = new Dictionary<string, string>()
            };

            Assert.IsFalse(ctx.CanAccess(rule));
        }
    }
}
